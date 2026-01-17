using HotelManagement.Domain;
using HotelManagement.Repository.Common;
using HotelManagement.Domain.Repositories;
using HotelManagement.Services.Admin.Orders.Dtos;
using HotelManagement.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Services.Admin.Orders;

public class OrdersService : IOrdersService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IRepository<OrderItem> _orderItemRepository;
    private readonly IRepository<Hotel> _hotelRepository;
    private readonly IRepository<Booking> _bookingRepository;
    private readonly IRepository<MenuItem> _menuItemRepository;
    private readonly IRepository<OrderItemHistory> _orderItemHistoryRepository;
    private readonly IRepository<Guest> _guestRepo;
    private readonly IUnitOfWork _unitOfWork;

    public OrdersService(
        IRepository<Order> orderRepository,
        IRepository<OrderItem> orderItemRepository,
        IRepository<Hotel> hotelRepository,
        IRepository<Booking> bookingRepository,
        IRepository<MenuItem> menuItemRepository,
        IRepository<Guest> guestRepo,
        IRepository<OrderItemHistory> orderItemHistoryRepository,
        IUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _orderItemRepository = orderItemRepository;
        _hotelRepository = hotelRepository;
        _bookingRepository = bookingRepository;
        _menuItemRepository = menuItemRepository;
        _orderItemHistoryRepository = orderItemHistoryRepository;
        _unitOfWork = unitOfWork;
        _guestRepo = guestRepo;
    }

    public async Task<ApiResponse<List<OrderSummaryDto>>> ListAsync(OrdersQueryDto query)
    {
        try
        {
            var q = _orderRepository.Query()
                .Include(o => o.Items)
                .Where(x => true);

            if (query.HotelId.HasValue)
                q = q.Where(o => o.HotelId == query.HotelId.Value);

            //if (query.Status.HasValue)
            //    q = q.Where(o => o.Status == query.Status.Value);

            if (query.BookingId.HasValue)
                q = q.Where(o => o.BookingId == query.BookingId.Value);

            if (query.IsWalkIn.HasValue)
                q = q.Where(o => o.IsWalkIn == query.IsWalkIn.Value);

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                q = q.Where(o => (o.CustomerName ?? "").Contains(query.Search!) ||
                                  (o.CustomerPhone ?? "").Contains(query.Search!));
            }

            if (query.Status.HasValue)
            {
                q = q.Where(o => o.Status == query.Status);
            }

            var items = await q
                .OrderByDescending(o => o.CreatedAt)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();



            var dtos = new List<OrderSummaryDto>();

            foreach (var o in items)
            {
                var dto = new OrderSummaryDto
                {
                    Id = o.Id,
                    HotelId = o.HotelId,
                    BookingId = o.BookingId,
                    IsWalkIn = o.IsWalkIn,
                    CustomerName = o.CustomerName,
                    CustomerPhone = o.CustomerPhone,
                    Status = o.Status,
                    Notes = o.Notes,
                    ChangeFoodRequest = o.ChangeFoodRequest,
                    CreatedAt = o.CreatedAt,
                    ItemsCount = o.Items.Count,
                    PromotionCode = o.PromotionCode,
                    PromotionValue = o.PromotionValue ?? 0,
                    AdditionalValue = o.AdditionalValue ?? 0,
                    AdditionalNote = o.AdditionalNotes,
                    Guests = o.Guests,
                    ItemsTotal = o.Items
                        .Where(i => i.Status != OrderItemStatus.Voided)
                        .Sum(i => i.UnitPrice * i.Quantity) + (o.AdditionalValue ?? 0),
                    Items = await GetOrderItemsAsync(o.Id)
                };

                dtos.Add(dto);
            }

            return ApiResponse<List<OrderSummaryDto>>.Ok(dtos);
        }
        catch (Exception ex)
        {
            return ApiResponse<List<OrderSummaryDto>>.Fail($"Error listing orders: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<OrderSummaryDto>>> ListActiveAsync(OrdersQueryDto query)
    {
        try
        {
            var q = _orderRepository.Query()
                .Include(o => o.Items)
                .Where(x => true);

            if (query.HotelId.HasValue)
                q = q.Where(o => o.HotelId == query.HotelId.Value);

            if (query.BookingId.HasValue)
                q = q.Where(o => o.BookingId == query.BookingId.Value);

            if (query.IsWalkIn.HasValue)
                q = q.Where(o => o.IsWalkIn == query.IsWalkIn.Value);

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                q = q.Where(o => (o.CustomerName ?? "").Contains(query.Search!) ||
                                  (o.CustomerPhone ?? "").Contains(query.Search!));
            }

            if (query.Status.HasValue)
            {
                q = q.Where(o => o.Status == query.Status);
            }

            var items = await q
                .OrderByDescending(o => o.CreatedAt)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();



            var dtos = new List<OrderSummaryDto>();

            foreach (var o in items)
            {
                var dto = new OrderSummaryDto
                {
                    Id = o.Id,
                    HotelId = o.HotelId,
                    BookingId = o.BookingId,
                    IsWalkIn = o.IsWalkIn,
                    CustomerName = o.CustomerName,
                    CustomerPhone = o.CustomerPhone,
                    Status = o.Status,
                    Notes = o.Notes,
                    ChangeFoodRequest = o.ChangeFoodRequest,
                    CreatedAt = o.CreatedAt,
                    ItemsCount = o.Items.Count,
                    PromotionCode = o.PromotionCode,
                    PromotionValue = o.PromotionValue ?? 0,
                    AdditionalValue = o.AdditionalValue ?? 0,
                    AdditionalNote = o.AdditionalNotes,
                    Guests = o.Guests,
                    ItemsTotal = o.Items
                        .Where(i => i.Status != OrderItemStatus.Voided)
                        .Sum(i => i.UnitPrice * i.Quantity) + (o.AdditionalValue ?? 0),
                    Items = await GetOrderItemsAsync(o.Id)
                };

                dtos.Add(dto);
            }

            return ApiResponse<List<OrderSummaryDto>>.Ok(dtos);
        }
        catch (Exception ex)
        {
            return ApiResponse<List<OrderSummaryDto>>.Fail($"Error listing orders: {ex.Message}");
        }
    }

    private async Task<List<OrderItemDto>> GetOrderItemsAsync(Guid id)
    {
        try
        {
            var o = await _orderRepository.Query()
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id);
            if (o == null) return [];

            var itemIds = o.Items.Select(i => i.MenuItemId).ToList();
            var menuNames = await _menuItemRepository.Query()
                .Where(mi => itemIds.Contains(mi.Id))
                .Select(mi => new { mi.Id, mi.Name })
                .ToListAsync();
            var nameMap = menuNames.ToDictionary(x => x.Id, x => x.Name);

            var items = o.Items.Select(i => new OrderItemDto
            {
                Id = i.Id,
                MenuItemId = i.MenuItemId,
                MenuItemName = nameMap.TryGetValue(i.MenuItemId, out var n) ? n : string.Empty,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                Status = i.Status
            }).ToList();

            return items;
        }
        catch (Exception)
        {
            return [];
        }
    }

    public async Task<ApiResponse<OrderDetailsDto>> GetByIdAsync(Guid id)
    {
        try
        {
            var o = await _orderRepository.Query()
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id);
            if (o == null) return ApiResponse<OrderDetailsDto>.Fail("Order not found");

            var itemIds = o.Items.Select(i => i.MenuItemId).ToList();
            var menuNames = await _menuItemRepository.Query()
                .Where(mi => itemIds.Contains(mi.Id))
                .Select(mi => new { mi.Id, mi.Name })
                .ToListAsync();
            var nameMap = menuNames.ToDictionary(x => x.Id, x => x.Name);

            var dto = new OrderDetailsDto
            {
                Id = o.Id,
                HotelId = o.HotelId,
                BookingId = o.BookingId,
                IsWalkIn = o.IsWalkIn,
                CustomerName = o.CustomerName,
                CustomerPhone = o.CustomerPhone,
                Status = o.Status,
                Notes = o.Notes,
                ChangeFoodRequest = o.ChangeFoodRequest,
                CreatedAt = o.CreatedAt,
                ItemsCount = o.Items.Count,
                ServingDate = o.ServingDate,
                AdditionalNote = o.AdditionalNotes,
                AdditionalValue = o.AdditionalValue,
                ItemsTotal = o.Items.Where(i => i.Status != OrderItemStatus.Voided).Sum(i => i.UnitPrice * i.Quantity) + (o.AdditionalValue ?? 0),
                PromotionCode = o.PromotionCode,
                PromotionValue = o.PromotionValue ?? 0,
                Guests = o.Guests,
                Items = o.Items.Select(i => new OrderItemDto
                {
                    Id = i.Id,
                    MenuItemId = i.MenuItemId,
                    MenuItemName = nameMap.TryGetValue(i.MenuItemId, out var n) ? n : string.Empty,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    Status = i.Status
                }).ToList()
            };

            var histories = await _orderItemHistoryRepository.Query()
                .Where(h => h.OrderId == o.Id)
                .ToListAsync();
            if (histories.Count > 0)
            {
                var histMenuIds = histories.SelectMany(h => new[] { h.OldMenuItemId, h.NewMenuItemId }).Distinct().ToList();
                var histMenus = await _menuItemRepository.Query()
                    .Where(mi => histMenuIds.Contains(mi.Id))
                    .Select(mi => new { mi.Id, mi.Name })
                    .ToListAsync();
                var hNameMap = histMenus.ToDictionary(x => x.Id, x => x.Name);
                dto.ItemHistories = histories.Select(h => new OrderItemHistoryDto
                {
                    Id = h.Id,
                    OldOrderItemId = h.OldOrderItemId,
                    NewOrderItemId = h.NewOrderItemId,
                    OldMenuItemId = h.OldMenuItemId,
                    NewMenuItemId = h.NewMenuItemId,
                    OldMenuItemName = hNameMap.TryGetValue(h.OldMenuItemId, out var on) ? on : string.Empty,
                    NewMenuItemName = hNameMap.TryGetValue(h.NewMenuItemId, out var nn) ? nn : string.Empty,
                    ChangedAt = h.ChangedAt,
                    UserId = h.UserId,
                    Reason = h.Reason
                }).ToList();
            }

            return ApiResponse<OrderDetailsDto>.Ok(dto);
        }
        catch (Exception ex)
        {
            return ApiResponse<OrderDetailsDto>.Fail($"Error retrieving order: {ex.Message}");
        }
    }

    public async Task<ApiResponse<OrderDetailsDto>> CreateWalkInAsync(CreateWalkInOrderDto dto)
    {
        try
        {
            var hotel = await _hotelRepository.FindAsync(dto.HotelId);
            if (hotel == null) return ApiResponse<OrderDetailsDto>.Fail("Hotel not found");

            var order = new Order
            {
                Id = Guid.NewGuid(),
                HotelId = dto.HotelId,
                IsWalkIn = true,
                CustomerName = dto.CustomerName,
                CustomerPhone = dto.CustomerPhone,
                Status = OrderStatus.NeedConfirmed,
                CreatedAt = dto.ServingDate ?? DateTime.Now,
                ServingDate = dto.ServingDate,
                Notes = dto.Notes,
                Guests = dto.Guests,
            };
            await _orderRepository.AddAsync(order);

            if (dto.Items != null && dto.Items.Any())
            {
                foreach (var item in dto.Items)
                {
                    var menu = await _menuItemRepository.Query().FirstOrDefaultAsync(mi => mi.Id == item.MenuItemId && mi.HotelId == dto.HotelId && mi.IsActive);
                    if (menu == null) return ApiResponse<OrderDetailsDto>.Fail("Menu item not found or inactive");

                    await _orderItemRepository.AddAsync(new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        OrderId = order.Id,
                        MenuItemId = item.MenuItemId,
                        Quantity = item.Quantity,
                        Name = menu.Name,
                        UnitPrice = menu.UnitPrice,
                        Status = OrderItemStatus.Pending
                    });
                }
            }


            var existGuest = await _guestRepo.Query().Where(x => x.Phone == order.CustomerPhone).AnyAsync();
            if (!existGuest)
            {
                var newGuest = new Guest()
                {
                    HotelId = dto.HotelId,
                    Phone = order.CustomerName,
                    FullName = order.CustomerName,
                    IdCardBackImageUrl = string.Empty,
                    IdCardFrontImageUrl = string.Empty,
                    IdCard = string.Empty,
                };
                await _guestRepo.AddAsync(newGuest);
                await _guestRepo.SaveChangesAsync();
            }

            await _orderRepository.SaveChangesAsync();

            return await GetByIdAsync(order.Id);
        }
        catch (Exception ex)
        {
            return ApiResponse<OrderDetailsDto>.Fail($"Error creating walk-in order: {ex.Message}");
        }
    }

    public async Task<ApiResponse<OrderDetailsDto>> CreateForBookingAsync(CreateBookingOrderDto dto)
    {
        try
        {
            var booking = await _bookingRepository
                .Query()
                .Include(x => x.PrimaryGuest)
                .Where(b => b.Id == dto.BookingId && b.HotelId == dto.HotelId)
                .FirstOrDefaultAsync();
            if (booking == null) return ApiResponse<OrderDetailsDto>.Fail("Booking not found in hotel");

            var order = new Order
            {
                Id = Guid.NewGuid(),
                HotelId = dto.HotelId,
                BookingId = dto.BookingId,
                CustomerName = booking.PrimaryGuest?.FullName,
                CustomerPhone = booking.PrimaryGuest?.Phone,
                IsWalkIn = false,
                Notes = dto.Notes,
                Status = OrderStatus.NeedConfirmed,
                CreatedAt = dto.ServingDate ?? DateTime.Now,
                ServingDate = dto.ServingDate,
                Guests = dto.Guests,
            };
            await _orderRepository.AddAsync(order);

            if (dto.Items != null && dto.Items.Any())
            {
                foreach (var item in dto.Items)
                {
                    var menu = await _menuItemRepository.Query().FirstOrDefaultAsync(mi => mi.Id == item.MenuItemId && mi.HotelId == dto.HotelId && mi.IsActive);
                    if (menu == null) return ApiResponse<OrderDetailsDto>.Fail("Menu item not found or inactive");

                    await _orderItemRepository.AddAsync(new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        OrderId = order.Id,
                        MenuItemId = item.MenuItemId,
                        Quantity = item.Quantity,
                        Name = menu.Name,
                        UnitPrice = menu.UnitPrice,
                        Status = OrderItemStatus.Pending
                    });
                }
            }

            await _orderRepository.SaveChangesAsync();

            return await GetByIdAsync(order.Id);
        }
        catch (Exception ex)
        {
            return ApiResponse<OrderDetailsDto>.Fail($"Error creating booking order: {ex.Message}");
        }
    }


    public async Task<ApiResponse<OrderDetailsDto>> AddItemAsync(Guid orderId, AddOrderItemDto dto)
    {
        try
        {
            var order = await _orderRepository.FindAsync(orderId);
            if (order == null) return ApiResponse<OrderDetailsDto>.Fail("Order not found");

            var menu = await _menuItemRepository.Query().FirstOrDefaultAsync(mi => mi.Id == dto.MenuItemId && mi.HotelId == order.HotelId && mi.IsActive);
            if (menu == null) return ApiResponse<OrderDetailsDto>.Fail("Menu item not found or inactive");

            await _orderItemRepository.AddAsync(new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                MenuItemId = dto.MenuItemId,
                Quantity = dto.Quantity,
                UnitPrice = menu.UnitPrice,
                Name = menu.Name,
                Status = OrderItemStatus.Pending
            });

            await _orderRepository.SaveChangesAsync();
            return await GetByIdAsync(orderId);
        }
        catch (Exception ex)
        {
            return ApiResponse<OrderDetailsDto>.Fail($"Error adding order item: {ex.Message}");
        }
    }

    public async Task<ApiResponse<OrderDetailsDto>> UpdateItemAsync(Guid orderId, Guid itemId, UpdateOrderItemDto dto)
    {
        try
        {
            var item = await _orderItemRepository.Query().FirstOrDefaultAsync(i => i.Id == itemId && i.OrderId == orderId);
            if (item == null) return ApiResponse<OrderDetailsDto>.Fail("Order item not found");

            if (dto.Quantity.HasValue && dto.Quantity.Value > 0)
                item.Quantity = dto.Quantity.Value;

            if (dto.Status.HasValue)
                item.Status = dto.Status.Value;

            if (dto.ProposedReplacementMenuItemId.HasValue)
                item.ProposedReplacementMenuItemId = dto.ProposedReplacementMenuItemId.Value;

            if (dto.ReplacementConfirmedByGuest.HasValue)
                item.ReplacementConfirmedByGuest = dto.ReplacementConfirmedByGuest.Value;

            await _orderItemRepository.UpdateAsync(item);
            await _orderItemRepository.SaveChangesAsync();
            return await GetByIdAsync(orderId);
        }
        catch (Exception ex)
        {
            return ApiResponse<OrderDetailsDto>.Fail($"Error updating order item: {ex.Message}");
        }
    }

    public async Task<ApiResponse<OrderDetailsDto>> RemoveItemAsync(Guid orderId, Guid itemId)
    {
        try
        {
            var item = await _orderItemRepository.Query().FirstOrDefaultAsync(i => i.Id == itemId && i.OrderId == orderId);
            if (item == null) return ApiResponse<OrderDetailsDto>.Fail("Order item not found");

            if (item.Status != OrderItemStatus.Pending && item.Status != OrderItemStatus.Voided)
                return ApiResponse<OrderDetailsDto>.Fail("Only pending or voided items can be removed");

            await _orderItemRepository.RemoveAsync(item);
            await _orderItemRepository.SaveChangesAsync();
            return await GetByIdAsync(orderId);
        }
        catch (Exception ex)
        {
            return ApiResponse<OrderDetailsDto>.Fail($"Error removing order item: {ex.Message}");
        }
    }

    public async Task<ApiResponse<OrderDetailsDto>> ReplaceItemAsync(Guid orderId, Guid itemId, ReplaceOrderItemDto dto, Guid? userId)
    {
        try
        {
            var order = await _orderRepository.FindAsync(orderId);
            if (order == null) return ApiResponse<OrderDetailsDto>.Fail("Order not found");

            var oldItem = await _orderItemRepository.Query().FirstOrDefaultAsync(i => i.Id == itemId && i.OrderId == orderId);
            if (oldItem == null) return ApiResponse<OrderDetailsDto>.Fail("Order item not found");

            var newMenu = await _menuItemRepository.Query()
                .FirstOrDefaultAsync(mi => mi.Id == dto.NewMenuItemId && mi.HotelId == order.HotelId && mi.IsActive);
            if (newMenu == null) return ApiResponse<OrderDetailsDto>.Fail("Menu item not found or inactive");

            oldItem.Status = OrderItemStatus.Voided;
            await _orderItemRepository.UpdateAsync(oldItem);

            var newItem = new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                MenuItemId = newMenu.Id,
                Quantity = dto.Quantity.HasValue ? Math.Max(1, dto.Quantity.Value) : oldItem.Quantity,
                UnitPrice = newMenu.UnitPrice,
                Name = newMenu.Name,
                Status = OrderItemStatus.Pending
            };
            await _orderItemRepository.AddAsync(newItem);

            await _orderItemHistoryRepository.AddAsync(new OrderItemHistory
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                OldOrderItemId = oldItem.Id,
                NewOrderItemId = newItem.Id,
                OldMenuItemId = oldItem.MenuItemId,
                NewMenuItemId = newItem.MenuItemId,
                ChangedAt = DateTime.Now,
                UserId = userId,
                Reason = dto.Reason
            });

            await _unitOfWork.SaveChangesAsync();
            return await GetByIdAsync(orderId);
        }
        catch (Exception ex)
        {
            return ApiResponse<OrderDetailsDto>.Fail($"Error replacing order item: {ex.Message}");
        }
    }

    public async Task<ApiResponse<OrderDetailsDto>> UpdateWalkInAsync(Guid id, UpdateWalkInOrderDto dto)
    {
        try
        {
            var order = await _orderRepository.FindAsync(id);
            if (order == null) return ApiResponse<OrderDetailsDto>.Fail("Order not found");

            order.Notes = dto.Notes;
            order.CustomerPhone = dto.CustomerPhone;
            order.CustomerName = dto.CustomerName;
            order.Guests = dto.Guests;
            order.CreatedAt = dto.ServingDate ?? DateTime.Now;
            if (dto.Status.HasValue)
            {
                order.Status = dto.Status.Value;
            }

            if (dto.ServingDate.HasValue)
            {
                order.ServingDate = dto.ServingDate;
            }


            var oldItems = await _orderItemRepository.Query().Where(x => x.OrderId == dto.Id).ToListAsync();
            await _orderItemRepository.RemoveRangeAsync(oldItems);
            await _orderItemRepository.SaveChangesAsync();

            if (dto.Items != null && dto.Items.Any())
            {
                foreach (var item in dto.Items)
                {
                    var menu = await _menuItemRepository.Query().FirstOrDefaultAsync(mi => mi.Id == item.MenuItemId && mi.HotelId == dto.HotelId && mi.IsActive);
                    if (menu == null) return ApiResponse<OrderDetailsDto>.Fail("Menu item not found or inactive");

                    await _orderItemRepository.AddAsync(new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        OrderId = order.Id,
                        MenuItemId = item.MenuItemId,
                        Quantity = item.Quantity,
                        UnitPrice = menu.UnitPrice,
                        Name = menu.Name,
                        Status = OrderItemStatus.Pending
                    });
                }
            }


            await _orderRepository.UpdateAsync(order);
            await _orderRepository.SaveChangesAsync();
            return await GetByIdAsync(order.Id);
        }
        catch (Exception ex)
        {
            return ApiResponse<OrderDetailsDto>.Fail($"Error updating order: {ex.Message}");
        }
    }

    public async Task<ApiResponse<OrderDetailsDto>> UpdateForBookingAsync(Guid id, UpdateOrderForBookingDto dto)
    {
        try
        {
            var order = await _orderRepository.FindAsync(id);
            if (order == null) return ApiResponse<OrderDetailsDto>.Fail("Order not found");

            order.Notes = dto.Notes;
            order.BookingId = dto.BookingId;
            order.Guests = dto.Guests;

            if (dto.Status.HasValue)
            {
                order.Status = dto.Status.Value;
            }

            if (dto.ServingDate.HasValue)
            {
                order.ServingDate = dto.ServingDate;
            }


            var booking = await _bookingRepository
                .Query()
                .Include(x => x.PrimaryGuest)
                .Where(b => b.Id == dto.BookingId && b.HotelId == dto.HotelId)
                .FirstOrDefaultAsync();
            if (booking == null) return ApiResponse<OrderDetailsDto>.Fail("Booking not found in hotel");

            order.CustomerPhone = booking.PrimaryGuest?.Phone;
            order.CustomerName = booking.PrimaryGuest?.FullName;

            var oldItems = await _orderItemRepository.Query().Where(x => x.OrderId == dto.Id).ToListAsync();
            await _orderItemRepository.RemoveRangeAsync(oldItems);
            await _orderItemRepository.SaveChangesAsync();

            if (dto.Items != null && dto.Items.Any())
            {
                foreach (var item in dto.Items)
                {
                    var menu = await _menuItemRepository.Query().FirstOrDefaultAsync(mi => mi.Id == item.MenuItemId && mi.HotelId == dto.HotelId && mi.IsActive);
                    if (menu == null) return ApiResponse<OrderDetailsDto>.Fail("Menu item not found or inactive");

                    await _orderItemRepository.AddAsync(new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        OrderId = order.Id,
                        MenuItemId = item.MenuItemId,
                        Quantity = item.Quantity,
                        UnitPrice = menu.UnitPrice,
                        Name = menu.Name,
                        Status = OrderItemStatus.Pending
                    });
                }
            }

            await _orderRepository.UpdateAsync(order);
            await _orderRepository.SaveChangesAsync();
            return await GetByIdAsync(order.Id);
        }
        catch (Exception ex)
        {
            return ApiResponse<OrderDetailsDto>.Fail($"Error updating order: {ex.Message}");
        }
    }

    public async Task<bool> UpdateWalkPromotionAsync(Guid id, UpdateWalkInPromotionDto dto)
    {
        try
        {
            var order = await _orderRepository.FindAsync(id);
            if (order == null) return false;

            order.PromotionCode = dto.PromotionCode;
            order.PromotionValue = dto.PromotionValue;

            await _orderRepository.UpdateAsync(order);
            await _orderRepository.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    public async Task<ApiResponse<OrderDetailsDto>> UpdateStatusAsync(Guid id, UpdateOrderStatusDto dto)
    {
        try
        {
            var order = await _orderRepository.FindAsync(id);
            if (order == null) return ApiResponse<OrderDetailsDto>.Fail("Order not found");

            order.Status = dto.Status;
            if (dto.Notes != null)
            {
                order.ChangeFoodRequest = dto.Notes;
            }

            await _orderRepository.UpdateAsync(order);
            await _orderRepository.SaveChangesAsync();
            return await GetByIdAsync(order.Id);
        }
        catch (Exception ex)
        {
            return ApiResponse<OrderDetailsDto>.Fail($"Error updating order status: {ex.Message}");
        }
    }
}
