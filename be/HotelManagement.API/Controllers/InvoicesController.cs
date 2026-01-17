using HotelManagement.Domain;
using HotelManagement.Domain.Repositories;
using HotelManagement.Repository.Common;
using HotelManagement.Services.Admin.Bookings;
using HotelManagement.Services.Admin.Bookings.Dtos;
using HotelManagement.Services.Admin.Invoicing;
using HotelManagement.Services.Admin.Invoicing.Dtos;
using HotelManagement.Services.Admin.Orders;
using HotelManagement.Services.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Api.Controllers;

[ApiController]
[Route("api/invoices")]
//[Authorize]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;
    private readonly IOrdersService _ordersService;
    private readonly IBookingsService _bookingsService;
    private readonly IRepository<Promotion> _promotionRepository;
    private readonly IRepository<Invoice> _invoiceRepository;
    private readonly IUnitOfWork _unitOfWork;

    public InvoicesController(
        IInvoiceService invoiceService,
        IOrdersService ordersService,
        IBookingsService bookingsService,
        IRepository<Promotion> promotionRepository,
        IRepository<Invoice> invoiceRepository,
        IUnitOfWork unitOfWork)
    {
        _invoiceService = invoiceService;
        _ordersService = ordersService;
        _bookingsService = bookingsService;
        _promotionRepository = promotionRepository;
        _invoiceRepository = invoiceRepository;
        _unitOfWork = unitOfWork;
    }

    [HttpGet("revenue")]
    public async Task<ActionResult<ApiResponse<RevenueStatsDto>>> GetRevenue([FromQuery] RevenueQueryDto query)
    {
        var stats = await _invoiceService.GetRevenueAsync(query);
        return Ok(ApiResponse<RevenueStatsDto>.Ok(stats));
    }

    [HttpGet("revenue/breakdown")]
    public async Task<ActionResult<ApiResponse<RevenueBreakdownDto>>> GetRevenueBreakdown([FromQuery] RevenueQueryDto query)
    {
        var stats = await _invoiceService.GetRevenueBreakdownAsync(query);
        return Ok(ApiResponse<RevenueBreakdownDto>.Ok(stats));
    }

    [HttpGet("revenue/details")]
    public async Task<ActionResult<ApiResponse<List<RevenueDetailItemDto>>>> GetRevenueDetails([FromQuery] RevenueQueryDto query, [FromQuery] InvoiceLineSourceType? sourceType)
    {
        var list = await _invoiceService.GetRevenueDetailsAsync(query, sourceType);
        return Ok(ApiResponse<List<RevenueDetailItemDto>>.Ok(list));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<InvoiceDto>>>> List([FromQuery] InvoiceFilterDto filter)
    {
        var res = await _invoiceService.GetInvoicesAsync(filter);
        return Ok(ApiResponse<PagedResult<InvoiceDto>>.Ok(res));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<InvoiceDto>>> Get(Guid id)
    {
        try
        {
            var dto = await _invoiceService.GetInvoiceAsync(id);
            return Ok(ApiResponse<InvoiceDto>.Ok(dto));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<InvoiceDto>.Fail(ex.Message));
        }
    }

    [HttpPost("walk-in")]
    public async Task<ActionResult<ApiResponse<InvoiceDto>>> CreateWalkInInvoice([FromBody] CreateWalkInInvoiceRequest request)
    {
        var orderRes = await _ordersService.GetByIdAsync(request.OrderId);
        if (!orderRes.IsSuccess || orderRes.Data is null)
        {
            return BadRequest(ApiResponse<InvoiceDto>.Fail(orderRes.Message ?? "Order not found"));
        }
        var order = orderRes.Data;

        if (!string.IsNullOrWhiteSpace(request.DiscountCode))
        {
            var now = DateTime.Now;
            var promo = await _promotionRepository.Query()
                .Where(p => p.HotelId == order.HotelId && p.Code == request.DiscountCode && p.IsActive && p.StartDate <= now && p.EndDate >= now)
                .FirstOrDefaultAsync();
            if (promo != null && (promo.Scope ?? "").Trim().ToLower() == "booking")
            {
                return BadRequest(ApiResponse<InvoiceDto>.Fail("Invalid promotion for walk-in"));
            }
        }

        var lines = new List<CreateInvoiceLineDto>
        {
            new ()
            {
                Description = $"Doanh thu đơn đồ ăn {order.Id}",
                Amount = order.Items.Sum(x => x.Quantity * x.UnitPrice) + (request.AdditionalValue ?? 0),
                SourceType = InvoiceLineSourceType.Fnb,
                SourceId = order.Id
            }
        };

        var createDto = new CreateInvoiceDto
        {
            HotelId = order.HotelId,
            OrderId = order.Id,
            IsWalkIn = true,
            GuestId = null,
            Notes = $"Walk-in invoice for {order.CustomerName}",
            Lines = lines,
            AdditionalNotes = request.AdditionalNotes,
            AdditionalValue = request.AdditionalValue,

        };

        var uidClaim = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(uidClaim, out var userId);

        if (createDto.OrderId.HasValue)
        {
            await _invoiceService.RemoveLastOrderInvoiceAsync((Guid)createDto.OrderId);
            var createdDto = await _invoiceService.CreateInvoiceAsync(createDto, userId);
            return Ok(ApiResponse<InvoiceDto>.Ok(createdDto, "Created"));
        }
        return BadRequest(ApiResponse<InvoiceDto>.Fail("Invalid order"));

    }

    [HttpPost("booking")]
    public async Task<ActionResult<ApiResponse<InvoiceDto>>> CreateBookingInvoice([FromBody] CreateBookingInvoiceRequest request)
    {
        var checkoutDto = new CheckoutRequestDto
        {
            DiscountCode = request.DiscountCode,
            FinalPayment = request.FinalPayment,
            CheckoutTime = request.CheckoutTime,
            AdditionalAmount = request.AdditionalAmount,
            AdditionalNotes = request.AdditionalNotes,
            Notes = request.Notes,
            AdditionalBookingAmount = request.AdditionalBookingAmount,
            AdditionalBookingNotes = request.AdditionalNotes,
            TotalAmount = request.TotalAmount
        };


        var result = await _bookingsService.CheckOutAsync(request.BookingId, checkoutDto);
        if (!result.IsSuccess)
        {
            return BadRequest(ApiResponse<InvoiceDto>.Fail(result.Message ?? "Checkout failed"));
        }



        if (await _invoiceService.AllowAddBookingInvoiceAsync(request.BookingId))
        {
            await _invoiceService.RemoveLastBookingInvoiceAsync(request.BookingId);
            await _bookingsService.AddBookingInvoiceAsync(request.BookingId, checkoutDto);

        }

        var inv = _invoiceRepository.Query()
             .Where(i => i.BookingId == request.BookingId)
             .OrderByDescending(i => i.CreatedAt)
             .FirstOrDefault();


        if (inv != null)
        {
            var dto = await _invoiceService.GetInvoiceAsync(inv.Id);
            return Ok(ApiResponse<InvoiceDto>.Ok(dto, "Created"));
        }

        return NotFound(ApiResponse<InvoiceDto>.Fail("not found"));
    }

}

public class CreateWalkInInvoiceRequest
{
    public Guid OrderId { get; set; }
    public string? DiscountCode { get; set; }
    public decimal? AdditionalValue { get; set; }
    public string? AdditionalNotes { get; set; }
}

public class CreateBookingInvoiceRequest
{
    public Guid BookingId { get; set; }
    public string? DiscountCode { get; set; }
    public string? Notes { get; set; }
    public string? AdditionalNotes { get; set; }
    public decimal? AdditionalAmount { get; set; }
    public decimal? AdditionalBookingAmount { get; set; }
    public string? AdditionalBookingNotes { get; set; }
    public decimal? TotalAmount { get; set; }
    public PaymentDto? FinalPayment { get; set; }
    public DateTime? CheckoutTime { get; set; }
}
