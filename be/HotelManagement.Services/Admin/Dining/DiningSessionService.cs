using HotelManagement.Domain;
using HotelManagement.Domain.Entities;
using HotelManagement.Domain.Repositories;
using HotelManagement.Repository.Common;
using HotelManagement.Services.Admin.Dining.Dtos;
using HotelManagement.Services.Admin.Orders.Dtos;
using HotelManagement.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Services.Admin.Dining;

public class DiningSessionService : IDiningSessionService
{
    private readonly IRepository<DiningSession> _diningSessionRepository;
    private readonly IRepository<Table> _tableRepository;
    private readonly IRepository<DiningSessionTable> _diningSessionTableRepository;
    private readonly IRepository<AppUser> _userRepository;
    private readonly IRepository<Order> _orderRepository;
    private readonly IRepository<MenuItem> _menuItemRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DiningSessionService(
        IRepository<DiningSession> diningSessionRepository,
        IRepository<Table> tableRepository,
        IRepository<DiningSessionTable> diningSessionTableRepository,
        IRepository<AppUser> userRepository,
        IRepository<Order> orderRepository,
        IRepository<MenuItem> menuItemRepository,
        IUnitOfWork unitOfWork)
    {
        _diningSessionRepository = diningSessionRepository;
        _tableRepository = tableRepository;
        _diningSessionTableRepository = diningSessionTableRepository;
        _userRepository = userRepository;
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _menuItemRepository = menuItemRepository;
    }

    public async Task<ApiResponse<DiningSessionDto>> CreateSessionAsync(CreateDiningSessionRequest request)
    {
        var session = new DiningSession
        {
            Id = Guid.NewGuid(),
            HotelId = request.HotelId,
            TableId = null,
            StartedAt = request.StartedAt ?? DateTime.Now,
            Notes = request.Notes ?? string.Empty,
            TotalGuests = request.TotalGuests ?? 0,
            Status = DiningSessionStatus.Open
        };

        await _diningSessionRepository.AddAsync(session);
        await _diningSessionRepository.SaveChangesAsync();

        return ApiResponse<DiningSessionDto>.Success(await MapToDto(session));
    }

    public async Task<ApiResponse<DiningSessionDto>> GetSessionAsync(Guid id)
    {
        var session = await _diningSessionRepository.FindAsync(id);
        if (session == null)
        {
            return ApiResponse<DiningSessionDto>.Fail("Dining session not found");
        }

        return ApiResponse<DiningSessionDto>.Success(await MapToDto(session));
    }

    public async Task<ApiResponse<DiningSessionListResponse>> GetSessionsAsync(Guid hotelId, int page = 1, int pageSize = 10, string? status = null)
    {
        var query = _diningSessionRepository.Query()
            .Where(s => s.HotelId == hotelId);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<DiningSessionStatus>(status, true, out var sessionStatus))
        {
            query = query.Where(s => s.Status == sessionStatus);
        }

        var totalCount = await query.CountAsync();
        var sessions = await query
            .OrderByDescending(s => s.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var dtos = new List<DiningSessionDto>();
        foreach (var session in sessions)
        {
            dtos.Add(await MapToDto(session));
        }

        return ApiResponse<DiningSessionListResponse>.Success(new DiningSessionListResponse
        {
            Sessions = dtos,
            TotalCount = totalCount
        });
    }

    public async Task<ApiResponse<DiningSessionDto>> UpdateSessionAsync(Guid id, UpdateDiningSessionRequest request)
    {
        var session = await _diningSessionRepository.FindAsync(id);
        if (session == null)
        {
            return ApiResponse<DiningSessionDto>.Fail("Dining session not found");
        }

        if (request.WaiterUserId.HasValue)
        {
            var waiter = await _userRepository.FindAsync(request.WaiterUserId.Value);
            if (waiter == null)
            {
                return ApiResponse<DiningSessionDto>.Fail("Waiter not found");
            }
            session.WaiterUserId = request.WaiterUserId;
        }

        if (!string.IsNullOrEmpty(request.Status) && Enum.TryParse<DiningSessionStatus>(request.Status, true, out var status))
        {
            session.Status = status;
            if (status == DiningSessionStatus.Closed)
            {
                session.EndedAt = DateTime.Now;
            }
        }

        if (request.Notes != null)
        {
            session.Notes = request.Notes;
        }
        if (request.TotalGuests.HasValue)
        {
            session.TotalGuests = request.TotalGuests.Value;
        }
        if (request.StartedAt.HasValue)
        {
            session.StartedAt = request.StartedAt.Value;
        }

        await _diningSessionRepository.UpdateAsync(session);
        await _diningSessionRepository.SaveChangesAsync();

        return ApiResponse<DiningSessionDto>.Success(await MapToDto(session));
    }

    public async Task<ApiResponse<bool>> EndSessionAsync(Guid id)
    {
        var session = await _diningSessionRepository.FindAsync(id);
        if (session == null)
        {
            return ApiResponse<bool>.Fail("Dining session not found");
        }

        session.Status = DiningSessionStatus.Closed;
        session.EndedAt = DateTime.Now;

        var linkedTables = await _diningSessionTableRepository.Query()
            .Where(x => x.DiningSessionId == id)
            .ToListAsync();
        foreach (var link in linkedTables)
        {
            var table = await _tableRepository.FindAsync(link.TableId);
            if (table != null)
            {
                table.TableStatus = 0;
                await _tableRepository.UpdateAsync(table);
                await _tableRepository.SaveChangesAsync();
            }
            await _diningSessionTableRepository.RemoveAsync(link);
            await _diningSessionTableRepository.SaveChangesAsync();
        }

        await _diningSessionRepository.UpdateAsync(session);
        await _diningSessionRepository.SaveChangesAsync();

        return ApiResponse<bool>.Success(true);
    }

    public async Task<ApiResponse<bool>> AttachTableAsync(Guid sessionId, Guid tableId)
    {
        var session = await _diningSessionRepository.FindAsync(sessionId);
        if (session == null || session.Status != DiningSessionStatus.Open)
        {
            return ApiResponse<bool>.Fail("Session not found or not open");
        }
        var table = await _tableRepository.FindAsync(tableId);
        if (table == null || table.TableStatus == 1)
        {
            return ApiResponse<bool>.Fail("Table not available");
        }

        var existing = await _diningSessionTableRepository.Query()
            .Where(x => x.TableId == tableId)
            .FirstOrDefaultAsync();
        if (existing != null)
        {
            return ApiResponse<bool>.Fail("Table is already attached");
        }

        var link = new DiningSessionTable
        {
            Id = Guid.NewGuid(),
            HotelId = session.HotelId,
            DiningSessionId = sessionId,
            TableId = tableId,
            AttachedAt = DateTime.Now,
        };
        await _diningSessionTableRepository.AddAsync(link);
        await _diningSessionTableRepository.SaveChangesAsync();

        table.TableStatus = 1;
        await _tableRepository.UpdateAsync(table);
        await _tableRepository.SaveChangesAsync();
        return ApiResponse<bool>.Success(true);
    }

    public async Task<ApiResponse<bool>> DetachTableAsync(Guid sessionId, Guid tableId)
    {
        var session = await _diningSessionRepository.FindAsync(sessionId);
        if (session == null)
        {
            return ApiResponse<bool>.Fail("Session not found");
        }
        var link = await _diningSessionTableRepository.Query()
            .Where(x => x.DiningSessionId == sessionId && x.TableId == tableId)
            .FirstOrDefaultAsync();
        if (link == null)
        {
            return ApiResponse<bool>.Fail("Link not found");
        }
        await _diningSessionTableRepository.RemoveAsync(link);
        await _diningSessionTableRepository.SaveChangesAsync();
        var table = await _tableRepository.FindAsync(tableId);
        if (table != null)
        {
            table.TableStatus = 0;
            await _tableRepository.UpdateAsync(table);
            await _tableRepository.SaveChangesAsync();
        }
        await _diningSessionTableRepository.SaveChangesAsync();
        return ApiResponse<bool>.Success(true);
    }

    public async Task<ApiResponse<bool>> DeleteSessionAsync(Guid id)
    {
        var session = await _diningSessionRepository.FindAsync(id);
        if (session == null)
        {
            return ApiResponse<bool>.Fail("Dining session not found");
        }

        var links = await _diningSessionTableRepository.Query()
            .Where(x => x.DiningSessionId == id)
            .ToListAsync();
        foreach (var link in links)
        {
            var table = await _tableRepository.FindAsync(link.TableId);
            if (table != null)
            {
                table.TableStatus = 0;
                await _tableRepository.UpdateAsync(table);
                await _tableRepository.SaveChangesAsync();
            }
            await _diningSessionTableRepository.RemoveAsync(link);
            await _diningSessionTableRepository.SaveChangesAsync();
        }

        var o = await _orderRepository.Query()
              .Include(o => o.Items)
              .Where(x => x.DiningSessionId == session.Id)
              .FirstOrDefaultAsync();
        if (o != null)
        {
            o.DiningSessionId = null;
            await _orderRepository.UpdateAsync(o);
            await _orderRepository.SaveChangesAsync();
        }



        await _diningSessionRepository.RemoveAsync(session);
        await _diningSessionRepository.SaveChangesAsync();
        return ApiResponse<bool>.Success(true);
    }

    public async Task<ApiResponse<bool>> UpdateSessionTablesAsync(Guid sessionId, UpdateSessionTablesRequest request)
    {
        var session = await _diningSessionRepository.FindAsync(sessionId);
        if (session == null || session.Status != DiningSessionStatus.Open)
        {
            return ApiResponse<bool>.Fail("Session not found or not open");
        }

        var attachIds = (request.AttachTableIds ?? new List<Guid>()).Distinct().ToList();
        var detachIds = (request.DetachTableIds ?? new List<Guid>()).Distinct().ToList();

        int changes = 0;

        foreach (var tableId in detachIds)
        {
            var link = await _diningSessionTableRepository.Query()
                .Where(x => x.DiningSessionId == sessionId && x.TableId == tableId)
                .FirstOrDefaultAsync();
            if (link != null)
            {
                await _diningSessionTableRepository.RemoveAsync(link);
                await _diningSessionTableRepository.SaveChangesAsync();

                var table = await _tableRepository.FindAsync(tableId);
                if (table != null)
                {
                    table.TableStatus = 0;
                    await _tableRepository.UpdateAsync(table);
                    await _tableRepository.SaveChangesAsync();
                }
                changes++;
            }
        }

        foreach (var tableId in attachIds)
        {
            var table = await _tableRepository.FindAsync(tableId);
            if (table == null || table.TableStatus == 1)
            {
                continue;
            }
            var existingLink = await _diningSessionTableRepository.Query()
                .Where(x => x.TableId == tableId)
                .FirstOrDefaultAsync();
            if (existingLink != null)
            {
                continue;
            }
            var link = new DiningSessionTable
            {
                Id = Guid.NewGuid(),
                HotelId = session.HotelId,
                DiningSessionId = sessionId,
                TableId = tableId,
                AttachedAt = DateTime.Now,
            };
            await _diningSessionTableRepository.AddAsync(link);
            await _diningSessionTableRepository.SaveChangesAsync();

            table.TableStatus = 1;
            await _tableRepository.UpdateAsync(table);
            await _tableRepository.SaveChangesAsync();
            changes++;
        }

        if (changes == 0)
        {
            return ApiResponse<bool>.Fail("No changes applied");
        }
        return ApiResponse<bool>.Success(true);
    }

    public async Task<ApiResponse<bool>> AssignOrderAsync(Guid sessionId, Guid orderId)
    {
        var session = await _diningSessionRepository.FindAsync(sessionId);
        if (session == null || session.Status != DiningSessionStatus.Open)
        {
            return ApiResponse<bool>.Fail("Session not found or not open");
        }

        var oldOrder = await _orderRepository.Query().Where(x => x.DiningSessionId == sessionId).FirstOrDefaultAsync();
        if(oldOrder != null)
        {
            oldOrder.DiningSessionId = null;
            await _orderRepository.UpdateAsync(oldOrder);
            await _orderRepository.SaveChangesAsync();
        }


        var order = await _orderRepository.FindAsync(orderId);
        if (order == null)
        {
            return ApiResponse<bool>.Fail("Order not found");
        }
        if (order.HotelId != session.HotelId)
        {
            return ApiResponse<bool>.Fail("Hotel mismatch");
        }

        order.DiningSessionId = sessionId;

        await _orderRepository.UpdateAsync(order);
        await _orderRepository.SaveChangesAsync();
        return ApiResponse<bool>.Success(true);
    }

    public async Task<ApiResponse<List<SessionTableDto>>> GetTablesBySessionAsync(Guid sessionId)
    {
        try
        {
            var session = await _diningSessionRepository.FindAsync(sessionId);
            if (session == null) return ApiResponse<List<SessionTableDto>>.Fail("Dining session not found");

            var links = await _diningSessionTableRepository.Query()
                .Where(x => x.DiningSessionId == sessionId)
                .OrderBy(x => x.AttachedAt)
                .ToListAsync();

            var dtos = new List<SessionTableDto>();
            foreach (var link in links)
            {
                var table = await _tableRepository.FindAsync(link.TableId);
                if (table != null)
                {
                    dtos.Add(new SessionTableDto
                    {
                        TableId = table.Id,
                        TableName = table.Name,
                        Capacity = table.Capacity,
                        AttachedAt = link.AttachedAt,
                    });
                }
            }

            return ApiResponse<List<SessionTableDto>>.Ok(dtos);
        }
        catch (Exception ex)
        {
            return ApiResponse<List<SessionTableDto>>.Fail($"Error retrieving tables: {ex.Message}");
        }
    }

    private async Task<DiningSessionDto> MapToDto(DiningSession session)
    {
        string? waiterName = null;
        string? wainterPhone = null;
        if (session.WaiterUserId.HasValue)
        {
            var waiter = await _userRepository.FindAsync(session.WaiterUserId.Value);
            waiterName = waiter?.Fullname;
            wainterPhone = waiter?.PhoneNumber;
        }

        var links = await _diningSessionTableRepository.Query()
            .Where(x => x.DiningSessionId == session.Id)
            .ToListAsync();
        var tableDtos = new List<SessionTableDto>();
        foreach (var link in links)
        {
            var table = await _tableRepository.FindAsync(link.TableId);
            if (table != null)
            {
                tableDtos.Add(new SessionTableDto
                {
                    TableId = table.Id,
                    TableName = table.Name,
                    Capacity = table.Capacity,
                    AttachedAt = link.AttachedAt,
                });
            }
        }

        return new DiningSessionDto
        {
            Id = session.Id,
            HotelId = session.HotelId,
            WaiterUserId = session.WaiterUserId,
            WaiterName = waiterName,
            WaiterPhoneNumber = wainterPhone,
            StartedAt = session.StartedAt,
            EndedAt = session.EndedAt,
            Status = session.Status.ToString(),
            Notes = session.Notes,
            TotalGuests = session.TotalGuests,
            Tables = tableDtos,
        };
    }

    public async Task<ApiResponse<OrderDetailsDto>> GetOrderOfSessionAsync(Guid sessionId)
    {
        try
        {
            var o = await _orderRepository.Query()
                .Include(o => o.Items)
                .Where(x => x.DiningSessionId == sessionId)
                .FirstOrDefaultAsync();
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
                CreatedAt = o.CreatedAt,
                ItemsCount = o.Items.Count,
                ServingDate = o.ServingDate,
                ItemsTotal = o.Items.Where(i => i.Status != OrderItemStatus.Voided).Sum(i => i.UnitPrice * i.Quantity),
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

            return ApiResponse<OrderDetailsDto>.Ok(dto);
        }
        catch (Exception ex)
        {
            return ApiResponse<OrderDetailsDto>.Fail($"Error retrieving order: {ex.Message}");
        }
    }


    public async Task<ApiResponse<OrderDetailsDto>> GetOrderOfTableAsync(Guid tableId)
    {
        try
        {
            var table = await _diningSessionTableRepository.Query()
                .OrderByDescending(x => x.AttachedAt)
                .Where(x => x.TableId == tableId)
                .FirstOrDefaultAsync();

            if (table == null) return ApiResponse<OrderDetailsDto>.Fail("Không tìm thấy bàn");

            var o = await _orderRepository.Query()
                .Include(o => o.Items)
                .Where(x => x.DiningSessionId == table.DiningSessionId)
                .FirstOrDefaultAsync();
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
                CreatedAt = o.CreatedAt,
                ItemsCount = o.Items.Count,
                ServingDate = o.ServingDate,
                ItemsTotal = o.Items.Where(i => i.Status != OrderItemStatus.Voided).Sum(i => i.UnitPrice * i.Quantity),
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

            return ApiResponse<OrderDetailsDto>.Ok(dto);
        }
        catch (Exception ex)
        {
            return ApiResponse<OrderDetailsDto>.Fail($"Error retrieving order: {ex.Message}");
        }
    }

    public async Task<(bool, string)> AssignWaiterAsync(AssignWaiterRequest request)
    {
        var session = await _diningSessionRepository.FindAsync(request.SessionId);
        if (session == null) return (false, "Không tìm thấy phiên phục vụ");

        var openAssignedCount = await _diningSessionRepository
            .Query()
            .Where(x => x.WaiterUserId == request.WaiterId)
            .Where(x => x.Status == DiningSessionStatus.Open)
            .CountAsync();

        if (openAssignedCount >= 3)
            return (false, "Mỗi nhân viên phục vụ chỉ được chỉ định tối đa 3 phiên đang mở.");

        session.WaiterUserId = request.WaiterId;
        await _diningSessionRepository.UpdateAsync(session);
        await _diningSessionRepository.SaveChangesAsync();

        return (true, "Gán nhân viên phục vụ thành công");
    }
}
