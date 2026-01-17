using HotelManagement.Domain;
using HotelManagement.Domain.Repositories;
using HotelManagement.Repository.Common;
using HotelManagement.Services.Admin.Bookings.Dtos;
using HotelManagement.Services.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
using System.Linq;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using System.Text.Json;
using HotelManagement.Services.Admin.RoomTypes.Dtos;

namespace HotelManagement.Services.Admin.Bookings;

public class BookingsService(
    IRepository<Booking> bookingRepo,
    IRepository<BookingRoomType> bookingRoomTypeRepo,
    IRepository<BookingRoom> bookingRoomRepo,
    IRepository<BookingGuest> bookingGuestRepo,
    IRepository<Hotel> hotelRepo,
    IRepository<Guest> guestRepo,
    IRepository<HotelRoom> roomRepo,
    IRepository<RoomType> roomTypeRepo,
    IRepository<CallLog> callLogRepo,
    IRepository<RoomStatusLog> roomStatusLogRepo,
    IRepository<SurchargeRule> surchargeRuleRepo,
    IRepository<Invoice> invoiceRepo,
    IRepository<InvoiceLine> invoiceLineRepo,
    IRepository<HotelManagement.Domain.Minibar> minibarRepo,
    IRepository<HotelManagement.Domain.MinibarBooking> minibarBookingRepo,
    IRepository<Promotion> promotionRepo,
    IUnitOfWork uow) : IBookingsService
{
    private readonly IRepository<Booking> _bookingRepo = bookingRepo;
    private readonly IRepository<BookingRoomType> _bookingRoomTypeRepo = bookingRoomTypeRepo;
    private readonly IRepository<BookingRoom> _bookingRoomRepo = bookingRoomRepo;
    private readonly IRepository<BookingGuest> _bookingGuestRepo = bookingGuestRepo;
    private readonly IRepository<Hotel> _hotelRepo = hotelRepo;
    private readonly IRepository<Guest> _guestRepo = guestRepo;
    private readonly IRepository<HotelRoom> _roomRepo = roomRepo;
    private readonly IRepository<RoomType> _roomTypeRepo = roomTypeRepo;
    private readonly IRepository<CallLog> _callLogRepo = callLogRepo;
    private readonly IRepository<RoomStatusLog> _roomStatusLogRepo = roomStatusLogRepo;
    private readonly IRepository<SurchargeRule> _surchargeRuleRepo = surchargeRuleRepo;
    private readonly IRepository<Invoice> _invoiceRepo = invoiceRepo;
    private readonly IRepository<InvoiceLine> _invoiceLineRepo = invoiceLineRepo;
    private readonly IRepository<HotelManagement.Domain.Minibar> _minibarRepo = minibarRepo;
    private readonly IRepository<HotelManagement.Domain.MinibarBooking> _minibarBookingRepo = minibarBookingRepo;
    private readonly IRepository<Promotion> _promotionRepo = promotionRepo;
    private readonly IUnitOfWork _uow = uow;

    public async Task<ApiResponse<BookingDetailsDto>> CreateAsync(CreateBookingDto dto)
    {
        try
        {
            var hotel = await _hotelRepo.FindAsync(dto.HotelId);
            if (hotel == null) return ApiResponse<BookingDetailsDto>.Fail("Không tìm thấy khách sạn");

            if (dto.RoomTypes == null || dto.RoomTypes.Count == 0)
                return ApiResponse<BookingDetailsDto>.Fail("Điền ít nhất 1 loại phòng");

            // Create primary guest
            var primaryGuest = new Guest
            {
                Id = Guid.NewGuid(),
                FullName = dto.PrimaryGuest.Fullname,
                Phone = dto.PrimaryGuest.Phone ?? string.Empty,
                Email = dto.PrimaryGuest.Email,
                HotelId = dto.HotelId
            };
            await _guestRepo.AddAsync(primaryGuest);
            await _guestRepo.SaveChangesAsync();

            await _uow.BeginTransactionAsync();

            var booking = new Booking
            {
                Id = Guid.NewGuid(),
                HotelId = dto.HotelId,
                PrimaryGuestId = primaryGuest.Id,
                Status = BookingStatus.Pending,
                DepositAmount = dto.Deposit,
                DiscountAmount = dto.Discount,
                TotalAmount = dto.Total,
                DefaultAmount = dto.Total,
                LeftAmount = dto.Left,
                CreatedAt = DateTime.Now,
                Notes = dto.Notes,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate
            };
            await _bookingRepo.AddAsync(booking);
            await _bookingRepo.SaveChangesAsync();


            foreach (var rt in dto.RoomTypes)
            {
                var roomType = await _roomTypeRepo.Query().FirstOrDefaultAsync(x => x.Id == rt.RoomTypeId && x.HotelId == dto.HotelId);
                if (roomType == null)
                {
                    await _uow.RollbackTransactionAsync();
                    return ApiResponse<BookingDetailsDto>.Fail("Room type not found in hotel");
                }

                var brt = new BookingRoomType
                {
                    BookingRoomTypeId = Guid.NewGuid(),
                    BookingId = booking.Id,
                    RoomTypeId = rt.RoomTypeId,
                    RoomTypeName = roomType.Name,
                    Capacity = roomType.Capacity,
                    Price = rt.Price ?? roomType.BasePriceFrom,
                    StartDate = rt.StartDate,
                    EndDate = rt.EndDate,
                    TotalRoom = rt.TotalRoom ?? 0

                };
                await _bookingRoomTypeRepo.AddAsync(brt);
                await _bookingRoomTypeRepo.SaveChangesAsync();

                if (rt.Rooms == null || rt.Rooms.Count == 0)
                {
                    continue;
                }

                foreach (var r in rt.Rooms)
                {
                    if (r.StartDate.Date >= r.EndDate.Date)
                    {
                        await _uow.RollbackTransactionAsync();
                        return ApiResponse<BookingDetailsDto>.Fail("Invalid date range for room");
                    }

                    var room = await _roomRepo.Query().FirstOrDefaultAsync(x => x.Id == r.RoomId && x.HotelId == dto.HotelId && x.RoomTypeId == rt.RoomTypeId);
                    if (room == null)
                    {
                        await _uow.RollbackTransactionAsync();
                        return ApiResponse<BookingDetailsDto>.Fail("Room not found in hotel or mismatched room type");
                    }

                    // Check availability (no overlapping confirmed/pending bookings)
                    var overlap = await _bookingRoomRepo.Query()
                        .Where(br => br.RoomId == r.RoomId && br.BookingStatus != BookingRoomStatus.Cancelled)
                        .AnyAsync(br => r.StartDate < br.EndDate && r.EndDate > br.StartDate);
                    if (overlap)
                    {
                        await _uow.RollbackTransactionAsync();
                        return ApiResponse<BookingDetailsDto>.Fail($"Room {room.Number} is not available for selected dates");
                    }

                    var bookingRoom = new BookingRoom
                    {
                        BookingRoomId = Guid.NewGuid(),
                        RoomId = r.RoomId,
                        BookingRoomTypeId = brt.BookingRoomTypeId,
                        RoomName = room.Number,
                        StartDate = r.StartDate,
                        EndDate = r.EndDate,
                        BookingStatus = BookingRoomStatus.Pending
                    };
                    await _bookingRoomRepo.AddAsync(bookingRoom);
                    await _bookingRoomRepo.SaveChangesAsync();

                    // Guests per room (optional), also link primary guest to first room of first type
                    var guests = r.Guests ?? new List<CreateBookingRoomGuestDto>();
                    if (!guests.Any())
                    {
                        // Attach primary guest by default
                        await _bookingGuestRepo.AddAsync(new BookingGuest
                        {
                            BookingGuestId = Guid.NewGuid(),
                            BookingRoomId = bookingRoom.BookingRoomId,
                            GuestId = primaryGuest.Id
                        });
                        await _bookingGuestRepo.SaveChangesAsync();
                    }
                    else
                    {
                        foreach (var g in guests)
                        {
                            Guid gid = g.GuestId ?? Guid.Empty;
                            if (gid == Guid.Empty)
                            {
                                var newG = new Guest
                                {
                                    Id = Guid.NewGuid(),
                                    FullName = g.Fullname ?? string.Empty,
                                    Phone = g.Phone ?? string.Empty,
                                    Email = g.Email
                                };
                                await _guestRepo.AddAsync(newG);
                                await _guestRepo.SaveChangesAsync();
                                gid = newG.Id;
                            }

                            await _bookingGuestRepo.AddAsync(new BookingGuest
                            {
                                BookingGuestId = Guid.NewGuid(),
                                BookingRoomId = bookingRoom.BookingRoomId,
                                GuestId = gid
                            });
                        }
                    }

                }
            }

            await _uow.CommitTransactionAsync();

            return await GetByIdAsync(booking.Id);
        }
        catch (Exception ex)
        {
            try { await _uow.RollbackTransactionAsync(); } catch { }
            return ApiResponse<BookingDetailsDto>.Fail($"Error creating booking: {ex.Message}");
        }
    }

    public async Task<ApiResponse<BookingDetailsDto>> GetByIdAsync(Guid id)
    {
        try
        {
            var b = await _bookingRepo.Query()
                .Include(x => x.PrimaryGuest)
                .Include(b => b.BookingRoomTypes)
                .ThenInclude(brt => brt.BookingRooms)
                .Include(b => b.CallLogs)
                .FirstOrDefaultAsync(b => b.Id == id);
            if (b == null) return ApiResponse<BookingDetailsDto>.Fail("Booking not found");

            // Load guests for rooms
            var roomIds = b.BookingRoomTypes.SelectMany(rt => rt.BookingRooms.Select(r => r.BookingRoomId)).ToList();
            var guests = await _bookingGuestRepo.Query()
                .Where(bg => roomIds.Contains(bg.BookingRoomId))
                .Select(bg => new { bg.BookingRoomId, bg.GuestId })
                .ToListAsync();
            var guestIds = guests.Select(g => g.GuestId).Distinct().ToList();
            var guestDetails = await _guestRepo.Query()
                .Where(g => guestIds.Contains(g.Id))
                .Select(g => new { g.Id, g.FullName, g.Phone, g.Email })
                .ToListAsync();
            var gm = guestDetails.ToDictionary(x => x.Id, x => x);



            var dto = new BookingDetailsDto
            {
                Id = b.Id,
                HotelId = b.HotelId,
                PrimaryGuestId = b.PrimaryGuestId,
                PrimaryGuestName = b.PrimaryGuest?.FullName,
                PhoneNumber = b.PrimaryGuest?.Phone,
                Email = b.PrimaryGuest?.Email,
                Notes = b.Notes,
                Status = b.Status,
                DefaultAmount = b.DefaultAmount,
                DepositAmount = b.DepositAmount,
                DiscountAmount = b.DiscountAmount,
                TotalAmount = b.TotalAmount,
                LeftAmount = b.LeftAmount,
                CreatedAt = b.CreatedAt,
                PromotionCode = b.PromotionCode,
                PromotionValue = b.PromotionValue,
                AdditionalBookingAmount = b.AdditionalBookingAmount ?? 0,
                AdditionalBookingNotes = b.AdditionalBookingNotes,
                CallLogs = b.CallLogs?.OrderByDescending(c => c.CallTime).Select(c => new CallLogDto
                {
                    Id = c.Id,
                    CallTime = c.CallTime,
                    Result = c.Result,
                    Notes = c.Notes,
                    StaffUserId = c.StaffUserId
                }).ToList() ?? []
            };

            var roomTypes = await _bookingRoomTypeRepo.Query()
                    .Include(x => x.RoomType).Include(x => x.BookingRooms)
                    .Where(x => x.BookingId == dto.Id).ToListAsync();

            var list = new List<BookingRoomTypeDto>();
            foreach (var rt in roomTypes)
            {
                var bookingRooms = await _bookingRoomRepo.Query()
                    .Include(x => x.HotelRoom)
                    .Where(x => x.BookingRoomTypeId == rt.BookingRoomTypeId)
                    .ToListAsync();

                var rtDto = new BookingRoomTypeDto()
                {
                    BookingRoomTypeId = rt.BookingRoomTypeId,
                    RoomTypeId = rt.RoomTypeId,
                    RoomTypeName = rt.RoomTypeName,
                    Capacity = rt.Capacity,
                    Price = rt.Price,
                    TotalRoom = rt.TotalRoom,
                    StartDate = rt.StartDate,
                    EndDate = rt.EndDate,
                };

                var listBookingRoomDto = new List<BookingRoomDto>();
                foreach (BookingRoom br in bookingRooms)
                {
                    var bookingGuests = await _bookingGuestRepo.Query()
                        .Include(x => x.BookingRoom)
                        .Include(x => x.Guest)
                        .Where(x => x.BookingRoomId == br.BookingRoomId).ToListAsync();

                    var bookingRoomDto = new BookingRoomDto
                    {
                        BookingRoomId = br.BookingRoomId,
                        RoomId = br.RoomId,
                        RoomName = br.RoomName,
                        StartDate = br.StartDate,
                        EndDate = br.EndDate,
                        BookingStatus = br.BookingStatus,
                        ActualCheckInAt = br.ActualCheckInAt,
                        ActualCheckOutAt = br.ActualCheckOutAt,
                        ExtendedDate = br.ExtendedDate,
                        Guests = [.. bookingGuests.Select(x => new BookingGuestDto()
                        {
                            GuestId = x.GuestId,
                            Fullname = x.Guest?.FullName,
                            Email = x.Guest?.Email,
                            Phone = x.Guest?.Phone,
                            IdCard = x.Guest?.IdCard,
                            IdCardBackImageUrl = x.Guest?.IdCardBackImageUrl,
                            IdCardFrontImageUrl = x.Guest?.IdCardFrontImageUrl
                        })]
                    };

                    listBookingRoomDto.Add(bookingRoomDto);
                }
                rtDto.BookingRooms = listBookingRoomDto;

                list.Add(rtDto);

            }

            dto.BookingRoomTypes = list;

            return ApiResponse<BookingDetailsDto>.Ok(dto);
        }
        catch (Exception ex)
        {
            return ApiResponse<BookingDetailsDto>.Fail($"Error retrieving booking: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<BookingDetailsDto>>> ListAsync(BookingsQueryDto query)
    {
        try
        {
            var q = _bookingRepo.Query()
                .Include(x => x.PrimaryGuest)
                .Include(b => b.BookingRoomTypes)
                .ThenInclude(rt => rt.BookingRooms)
                .Where(x => true);

            if (query.HotelId.HasValue)
                q = q.Where(b => b.HotelId == query.HotelId.Value);
            if (query.Status.HasValue)
                q = q.Where(b => b.Status == query.Status.Value);
            if (query.StartDate.HasValue)
                q = q.Where(b => b.StartDate >= query.StartDate.Value);
            if (query.EndDate.HasValue)
                q = q.Where(b => b.StartDate <= query.EndDate.Value);

            if (!string.IsNullOrWhiteSpace(query.GuestName))
            {
                var gn = query.GuestName!.Trim();
                q = q.Where(b => (_guestRepo.Query().Any(g => g.Id == b.PrimaryGuestId && ((g.FullName ?? "").Contains(gn) || g.Phone.Contains(gn)))));
            }
            if (!string.IsNullOrWhiteSpace(query.RoomNumber))
            {
                var rn = query.RoomNumber!.Trim();
                q = q.Where(b => b.BookingRoomTypes.Any(rt => rt.BookingRooms.Any(r => (r.RoomName ?? "").Contains(rn))));
            }

            // Sorting
            var sortDir = (query.SortDir ?? "desc").ToLower();
            var sortBy = (query.SortBy ?? "createdAt").ToLower();
            if (sortBy == "createdAt")
            {
                q = sortDir == "asc" ? q.OrderBy(b => b.CreatedAt) : q.OrderByDescending(b => b.CreatedAt);
            }
            else
            {
                q = q.OrderByDescending(b => b.CreatedAt);
            }

            var total = await q.CountAsync();
            var items = await q
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)

                .ToListAsync();

            // Preload guests
            var primaryGuestIds = items.Select(b => b.PrimaryGuestId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
            var guestMap = await _guestRepo.Query()
                .Where(g => primaryGuestIds.Contains(g.Id))
                .ToDictionaryAsync(g => g.Id, g => g.FullName);



            var dtos = items.Select(b => new BookingDetailsDto
            {
                Id = b.Id,
                HotelId = b.HotelId,
                PrimaryGuestId = b.PrimaryGuestId,
                PrimaryGuestName = b.PrimaryGuest?.FullName,
                PhoneNumber = b.PrimaryGuest?.Phone,
                Email = b.PrimaryGuest?.Email,
                Status = b.Status,
                DepositAmount = b.DepositAmount,
                DefaultAmount = b.DefaultAmount,
                DiscountAmount = b.DiscountAmount,
                TotalAmount = b.TotalAmount,
                LeftAmount = b.LeftAmount,
                CreatedAt = b.CreatedAt,
                Notes = b.Notes,
                AdditionalAmount = b.AdditionalAmount,
                AdditionalNotes = b.AdditionalNotes,
                PromotionValue = b.PromotionValue,
                PromotionCode = b.PromotionCode,
                StartDate = b.StartDate,
                EndDate = b.EndDate,
                AdditionalBookingNotes = b.AdditionalBookingNotes,
                AdditionalBookingAmount = b.AdditionalBookingAmount ?? 0,
                BookingRoomTypes = b.BookingRoomTypes.Select(rt => new BookingRoomTypeDto
                {
                    BookingRoomTypeId = rt.BookingRoomTypeId,
                    RoomTypeId = rt.RoomTypeId,
                    RoomTypeName = rt.RoomTypeName,
                    Capacity = rt.Capacity,
                    Price = rt.Price,
                    TotalRoom = rt.TotalRoom,
                    BookingRooms = rt.BookingRooms.Select(r => new BookingRoomDto
                    {
                        BookingRoomId = r.BookingRoomId,
                        RoomId = r.RoomId,
                        RoomName = r.RoomName,
                        StartDate = r.StartDate,
                        EndDate = r.EndDate,
                        BookingStatus = r.BookingStatus,
                        Guests = new List<BookingGuestDto>()
                    }).ToList()
                }).ToList(),
                CallLogs = new List<CallLogDto>()
            }).ToList();

            var list = new List<BookingDetailsDto>();

            foreach (var item in dtos)
            {
                var roomTypes = await _bookingRoomTypeRepo.Query()
                    .Include(x => x.RoomType)
                    .Include(x => x.BookingRooms)
                    .Where(x => x.BookingId == item.Id).ToListAsync();

                var listRoomType = new List<BookingRoomTypeDto>();
                foreach (var rt in roomTypes)
                {
                    var bookingRooms = await _bookingRoomRepo.Query()
                        .Include(x => x.HotelRoom)
                        .Where(x => x.BookingRoomTypeId == rt.BookingRoomTypeId)
                        .ToListAsync();

                    var rtDto = new BookingRoomTypeDto()
                    {
                        BookingRoomTypeId = rt.BookingRoomTypeId,
                        RoomTypeId = rt.RoomTypeId,
                        RoomTypeName = rt.RoomTypeName,
                        Capacity = rt.Capacity,
                        Price = rt.Price,
                        TotalRoom = rt.TotalRoom,
                        StartDate = rt.StartDate,
                        EndDate = rt.EndDate,
                    };

                    var listBookingRoomDto = new List<BookingRoomDto>();
                    foreach (BookingRoom br in bookingRooms)
                    {
                        var bookingGuests = await _bookingGuestRepo.Query()
                            .Include(x => x.BookingRoom)
                            .Include(x => x.Guest)
                            .Where(x => x.BookingRoomId == br.BookingRoomId).ToListAsync();

                        var bookingRoomDto = new BookingRoomDto
                        {
                            BookingRoomId = br.BookingRoomId,
                            RoomId = br.RoomId,
                            RoomName = br.RoomName,
                            StartDate = br.StartDate,
                            EndDate = br.ExtendedDate.HasValue ? br.ExtendedDate.Value : br.EndDate,
                            BookingStatus = br.BookingStatus,
                            ActualCheckInAt = br.ActualCheckInAt,
                            ActualCheckOutAt = br.ActualCheckOutAt,
                            ExtendedDate = br.ExtendedDate,
                            Guests = [.. bookingGuests.Select(x => new BookingGuestDto()
                        {
                            GuestId = x.GuestId,
                            Fullname = x.Guest?.FullName,
                            Email = x.Guest?.Email,
                            Phone = x.Guest?.Phone,
                            IdCard = x.Guest?.IdCard,
                            IdCardBackImageUrl = x.Guest?.IdCardBackImageUrl,
                            IdCardFrontImageUrl = x.Guest?.IdCardFrontImageUrl
                        })]
                        };

                        listBookingRoomDto.Add(bookingRoomDto);
                    }
                    rtDto.BookingRooms = listBookingRoomDto;

                    listRoomType.Add(rtDto);

                }
                item.BookingRoomTypes = listRoomType;

                list.Add(item);
            }
            return ApiResponse<List<BookingDetailsDto>>.Ok(list.OrderBy(x => x.Status).ThenBy(x => x.StartDate).ToList(), meta: new { total, page = query.Page, pageSize = query.PageSize });
        }
        catch (Exception ex)
        {
            return ApiResponse<List<BookingDetailsDto>>.Fail($"Error listing bookings: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<BookingDetailsDto>>> ListActiveAsync(BookingsByHotelQueryDto query)
    {
        try
        {
            var q = _bookingRepo.Query()
                .Include(x => x.PrimaryGuest)
                .Include(b => b.BookingRoomTypes)
                .ThenInclude(rt => rt.BookingRooms)
                .Where(x => x.Status != BookingStatus.Cancelled || x.Status != BookingStatus.Completed);

            if (query.HotelId.HasValue)
                q = q.Where(b => b.HotelId == query.HotelId.Value);

            q = q.OrderByDescending(b => b.CreatedAt);

            var items = await q.ToListAsync();

            // Preload guests
            var primaryGuestIds = items.Select(b => b.PrimaryGuestId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
            var guestMap = await _guestRepo.Query()
                .Where(g => primaryGuestIds.Contains(g.Id))
                .ToDictionaryAsync(g => g.Id, g => g.FullName);

            var dtos = items.Select(b => new BookingDetailsDto
            {
                Id = b.Id,
                HotelId = b.HotelId,
                PrimaryGuestId = b.PrimaryGuestId,
                PrimaryGuestName = b.PrimaryGuest?.FullName,
                PhoneNumber = b.PrimaryGuest?.Phone,
                Email = b.PrimaryGuest?.Email,
                Status = b.Status,
                DepositAmount = b.DepositAmount,
                DiscountAmount = b.DiscountAmount,
                TotalAmount = b.TotalAmount,
                LeftAmount = b.LeftAmount,
                CreatedAt = b.CreatedAt,
                StartDate = b.StartDate,
                EndDate = b.EndDate,
                Notes = b.Notes,
                BookingRoomTypes = b.BookingRoomTypes.Select(rt => new BookingRoomTypeDto
                {
                    BookingRoomTypeId = rt.BookingRoomTypeId,
                    RoomTypeId = rt.RoomTypeId,
                    RoomTypeName = rt.RoomTypeName,
                    Capacity = rt.Capacity,
                    Price = rt.Price,
                    TotalRoom = rt.TotalRoom,
                    BookingRooms = rt.BookingRooms.Select(r => new BookingRoomDto
                    {
                        BookingRoomId = r.BookingRoomId,
                        RoomId = r.RoomId,
                        RoomName = r.RoomName,
                        StartDate = r.StartDate,
                        EndDate = r.EndDate,
                        BookingStatus = r.BookingStatus,
                        Guests = new List<BookingGuestDto>()
                    }).ToList()
                }).ToList(),
                CallLogs = new List<CallLogDto>()
            }).ToList();

            var list = new List<BookingDetailsDto>();

            foreach (var item in dtos)
            {
                var roomTypes = await _bookingRoomTypeRepo.Query()
                    .Include(x => x.RoomType).Where(x => x.BookingId == item.Id).ToListAsync();

                item.BookingRoomTypes = roomTypes.Select(rt => new BookingRoomTypeDto
                {
                    BookingRoomTypeId = rt.BookingRoomTypeId,
                    RoomTypeId = rt.RoomTypeId,
                    RoomTypeName = rt.RoomTypeName,
                    Capacity = rt.Capacity,
                    Price = rt.Price,
                    TotalRoom = rt.TotalRoom,
                    BookingRooms = rt.BookingRooms.Select(r => new BookingRoomDto
                    {
                        BookingRoomId = r.BookingRoomId,
                        RoomId = r.RoomId,
                        RoomName = r.RoomName,
                        StartDate = r.StartDate,
                        EndDate = r.EndDate,
                        BookingStatus = r.BookingStatus,
                        Guests = new List<BookingGuestDto>()
                    }).ToList()
                }).ToList();

                list.Add(item);
            }


            return ApiResponse<List<BookingDetailsDto>>.Ok(list);
        }
        catch (Exception ex)
        {
            return ApiResponse<List<BookingDetailsDto>>.Fail($"Error listing bookings: {ex.Message}");
        }
    }

    public async Task<ApiResponse<BookingDetailsDto>> UpdateAsync(Guid id, UpdateBookingDto dto)
    {
        try
        {
            await _uow.BeginTransactionAsync();

            var booking = await _bookingRepo.FindAsync(id);
            if (booking == null)
            {
                await _uow.RollbackTransactionAsync();
                return ApiResponse<BookingDetailsDto>.Fail("Booking not found");
            }


            var isDateChanged = booking.StartDate != dto.StartDate || booking.EndDate != dto.EndDate;

            var existingBrtList = await _bookingRoomTypeRepo.Query()
                .Where(x => x.BookingId == booking.Id)
                .ToListAsync();

            var exist = existingBrtList.Where(x => dto.RoomTypes.Any(b => b.RoomTypeId == x.RoomTypeId)).ToList();
            var notExist = existingBrtList.Except(exist).ToList();

            foreach (var notExt in notExist)
            {
                await _bookingRoomTypeRepo.RemoveAsync(notExt);
                await _bookingRoomTypeRepo.SaveChangesAsync();
            }

            booking.BookingRoomTypes = exist;

            if (dto.RoomTypes != null && dto.RoomTypes.Count > 0)
            {
                foreach (var rtDto in dto.RoomTypes)
                {
                    var brt = booking.BookingRoomTypes.FirstOrDefault(x => x.RoomTypeId == rtDto.RoomTypeId);
                    if (brt == null)
                    {
                        var roomType = await _roomTypeRepo.FindAsync(rtDto.RoomTypeId);
                        if (roomType == null || roomType.HotelId != booking.HotelId)
                        {
                            await _uow.RollbackTransactionAsync();
                            return ApiResponse<BookingDetailsDto>.Fail("Room type invalid for hotel");
                        }
                        brt = new BookingRoomType
                        {
                            BookingRoomTypeId = Guid.NewGuid(),
                            BookingId = booking.Id,
                            RoomTypeId = rtDto.RoomTypeId,
                            RoomTypeName = roomType.Name,
                            Capacity = roomType.Capacity,
                            Price = rtDto.Price ?? roomType.BasePriceFrom,
                            StartDate = isDateChanged ? dto.StartDate!.Value : rtDto.StartDate,
                            EndDate = isDateChanged ? dto.EndDate!.Value : rtDto.EndDate,
                            TotalRoom = rtDto.TotalRoom ?? 0
                        };
                        await _bookingRoomTypeRepo.AddAsync(brt);
                        await _bookingRoomTypeRepo.SaveChangesAsync();
                        booking.BookingRoomTypes.Add(brt);
                    }
                    else
                    {
                        brt.StartDate = isDateChanged ? dto.StartDate!.Value : rtDto.StartDate;
                        brt.EndDate = isDateChanged ? dto.EndDate!.Value : rtDto.EndDate;
                        if (rtDto.Price.HasValue) brt.Price = rtDto.Price.Value;
                        await _bookingRoomTypeRepo.UpdateAsync(brt);
                        await _bookingRoomTypeRepo.SaveChangesAsync();
                    }

                    if (rtDto.TotalRoom.HasValue)
                    {
                        var current = brt.TotalRoom;
                        var target = rtDto.TotalRoom.Value;
                        if (target > current)
                        {
                            brt.TotalRoom = target;
                            await _bookingRoomTypeRepo.UpdateAsync(brt);
                            await _bookingRoomTypeRepo.SaveChangesAsync();
                        }
                        else if (target < current)
                        {
                            var diff = current - target;
                            var rooms = await _bookingRoomRepo.Query()
                                .Where(r => r.BookingRoomTypeId == brt.BookingRoomTypeId)
                                .ToListAsync();
                            var roomIds = rooms.Select(r => r.BookingRoomId).ToList();
                            var guestCounts = await _bookingGuestRepo.Query()
                                .Where(bg => roomIds.Contains(bg.BookingRoomId))
                                .GroupBy(bg => bg.BookingRoomId)
                                .Select(g => new { BookingRoomId = g.Key, Count = g.Count() })
                                .ToListAsync();
                            int GetGuestCount(Guid rid) => guestCounts.FirstOrDefault(x => x.BookingRoomId == rid)?.Count ?? 0;
                            var removable = rooms.Take(diff).ToList();

                            var remain = diff - removable.Count;
                            foreach (var r in removable)
                            {
                                var room = await _roomRepo.Query().Where(x => x.Id == r.RoomId).FirstOrDefaultAsync();
                                if (room is not null)
                                {
                                    room.Status = RoomStatus.Available;
                                    await _roomRepo.UpdateAsync(room);
                                    await _roomRepo.SaveChangesAsync();
                                }


                                r.BookingStatus = BookingRoomStatus.Cancelled;
                                await _bookingRoomRepo.RemoveAsync(r);
                                await _bookingRoomRepo.SaveChangesAsync();
                            }
                            await _bookingRoomRepo.SaveChangesAsync();
                            if (remain > 0)
                            {
                                var more = rooms.Where(r => r.BookingStatus != BookingRoomStatus.Cancelled && GetGuestCount(r.BookingRoomId) > 0)
                                                .Take(remain).ToList();
                                foreach (var r in more)
                                {
                                    r.BookingStatus = BookingRoomStatus.Cancelled;
                                    await _bookingRoomRepo.UpdateAsync(r);
                                }
                                await _bookingRoomRepo.SaveChangesAsync();
                            }
                            brt.TotalRoom = target;
                            await _bookingRoomTypeRepo.UpdateAsync(brt);
                            await _bookingRoomTypeRepo.SaveChangesAsync();
                        }
                    }
                }
            }


            booking.StartDate = dto.StartDate;
            booking.EndDate = dto.EndDate;
            booking.TotalAmount = dto.Total;
            booking.LeftAmount = dto.Left;
            booking.DefaultAmount = dto.Total;
            booking.DiscountAmount = dto.Discount;
            booking.DepositAmount = dto.Deposit;
            booking.Notes = dto.Notes;

            var guest = await _guestRepo.Query().FirstOrDefaultAsync(x => x.Id == booking.PrimaryGuestId);
            if (guest is not null)
            {
                guest.FullName = dto.PrimaryGuest.Fullname;
                guest.Phone = dto.PrimaryGuest.Phone ?? "";
                guest.Email = dto.PrimaryGuest.Email;
                await _guestRepo.SaveChangesAsync();
            }


            await _bookingRepo.UpdateAsync(booking);
            await _bookingRepo.SaveChangesAsync();


            var bookingRoomTypeIds = await _bookingRoomTypeRepo.Query().Where(x => x.BookingId == booking.Id).Select(x => x.RoomTypeId).ToListAsync();
            var bookingRoom = await _bookingRoomRepo.Query().Where(x => bookingRoomTypeIds.Contains(x.BookingRoomTypeId)).ToListAsync();

            foreach (var room in bookingRoom)
            {
                room.StartDate = booking.StartDate!.Value;
                room.EndDate = booking.EndDate!.Value;
                room.ExtendedDate = isDateChanged ? null : room.ExtendedDate;

                await _bookingRoomRepo.UpdateAsync(room);
                await _bookingRoomRepo.SaveChangesAsync();
            }

            await _uow.CommitTransactionAsync();
            return await GetByIdAsync(booking.Id);
        }
        catch (Exception ex)
        {
            try { await _uow.RollbackTransactionAsync(); } catch { }
            return ApiResponse<BookingDetailsDto>.Fail($"Error updating booking: {ex.Message}");
        }
    }

    public async Task<ApiResponse> CancelAsync(Guid id)
    {
        try
        {
            var booking = await _bookingRepo.FindAsync(id);
            if (booking == null) return ApiResponse.Fail("Booking not found");

            booking.Status = BookingStatus.Cancelled;
            await _bookingRepo.UpdateAsync(booking);
            await _bookingRepo.SaveChangesAsync();


            var bookingRoomTypes = await _bookingRoomTypeRepo.Query()
                .Include(x => x.BookingRooms)
                .Where(x => x.BookingId == booking.Id)
                .ToListAsync();
            booking.BookingRoomTypes = bookingRoomTypes;

            if (booking.BookingRoomTypes != null)
            {
                foreach (var roomType in booking.BookingRoomTypes)
                {
                    var bookingRooms = await _bookingRoomRepo.Query().Where(x => x.BookingRoomTypeId == roomType.BookingRoomTypeId).ToListAsync();

                    foreach (var r in bookingRooms)
                    {
                        r.BookingStatus = BookingRoomStatus.Cancelled;
                        await _bookingRoomRepo.UpdateAsync(r);
                        await _bookingRoomRepo.SaveChangesAsync();

                        var room = await _roomRepo.Query().Where(x => x.Id == r.RoomId).FirstOrDefaultAsync();
                        if (room == null) continue;

                        room.Status = RoomStatus.Available;
                        await _roomRepo.UpdateAsync(room);
                        await _roomRepo.SaveChangesAsync();

                    }
                }

            }

            return ApiResponse.Ok("Booking cancelled");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail($"Error cancelling booking: {ex.Message}");
        }
    }

    public async Task<ApiResponse> ConfirmAsync(Guid id)
    {
        try
        {
            var booking = await _bookingRepo.FindAsync(id);
            if (booking == null) return ApiResponse.Fail("Booking not found");

            booking.Status = BookingStatus.Confirmed;
            await _bookingRepo.UpdateAsync(booking);
            await _bookingRepo.SaveChangesAsync();
            return ApiResponse.Ok("Booking Confirmed");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail($"Error cancelling booking: {ex.Message}");
        }
    }

    public async Task<ApiResponse> CompleteAsync(Guid id)
    {
        try
        {
            var booking = await _bookingRepo.Query()
                .Include(b => b.BookingRoomTypes)
                .ThenInclude(rt => rt.BookingRooms)
                .Include(b => b.BookingRoomTypes)
                .ThenInclude(rt => rt.RoomType)
                .FirstOrDefaultAsync(b => b.Id == id);
            if (booking == null) return ApiResponse.Fail("Booking not found");

            decimal baseTotal = 0;

            var bookingRoomTypes = await _bookingRoomTypeRepo.Query().Where(x => x.BookingId == booking.Id).ToListAsync();

            foreach (var rt in bookingRoomTypes)
            {
                var bookingRooms = await _bookingRoomRepo.Query().Where(x => x.BookingRoomTypeId == rt.BookingRoomTypeId).ToListAsync();
                var roomType = rt.RoomType ?? await _roomTypeRepo.FindAsync(rt.RoomTypeId);
                var overrides = ParseOverrides(roomType?.Prices);
                foreach (var br in bookingRooms)
                {
                    var start = (br.ActualCheckInAt?.Date ?? br.StartDate.Date);
                    var endExclusive = (br.ActualCheckOutAt?.Date ?? br.EndDate.Date);
                    if (endExclusive <= start) endExclusive = start.AddDays(1);
                    for (var d = start; d < endExclusive; d = d.AddDays(1))
                    {
                        var match = overrides.FirstOrDefault(p => p.Date.Date == d);
                        var price = match?.Price ?? (roomType?.BasePriceFrom ?? rt.Price);
                        baseTotal += price;
                    }
                }
            }

            booking.Status = BookingStatus.Completed;
            booking.TotalAmount = baseTotal;
            booking.LeftAmount = Math.Max(0, booking.TotalAmount - booking.DepositAmount);
            await _bookingRepo.UpdateAsync(booking);
            await _bookingRepo.SaveChangesAsync();
            return ApiResponse.Ok("Booking Confirmed");
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail($"Error cancelling booking: {ex.Message}");
        }
    }

    private static List<PriceByDate> ParseOverrides(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<PriceByDate>();
        try
        {
            return JsonSerializer.Deserialize<List<PriceByDate>>(json) ?? new List<PriceByDate>();
        }
        catch
        {
            return new List<PriceByDate>();
        }
    }

    public async Task<ApiResponse<CallLogDto>> AddCallLogAsync(Guid bookingId, AddCallLogDto dto)
    {
        try
        {
            var booking = await _bookingRepo.FindAsync(bookingId);
            if (booking == null) return ApiResponse<CallLogDto>.Fail("Booking not found");

            var log = new CallLog
            {
                Id = Guid.NewGuid(),
                BookingId = bookingId,
                CallTime = dto.CallTime,
                Result = dto.Result,
                Notes = dto.Notes,
                StaffUserId = dto.StaffUserId
            };
            await _callLogRepo.AddAsync(log);
            await _callLogRepo.SaveChangesAsync();

            var dtoOut = new CallLogDto
            {
                Id = log.Id,
                CallTime = log.CallTime,
                Result = log.Result,
                Notes = log.Notes,
                StaffUserId = log.StaffUserId
            };
            return ApiResponse<CallLogDto>.Ok(dtoOut);
        }
        catch (Exception ex)
        {
            return ApiResponse<CallLogDto>.Fail($"Error adding call log: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<CallLogDto>>> GetCallLogsAsync(Guid bookingId)
    {
        try
        {
            var booking = await _bookingRepo.FindAsync(bookingId);
            if (booking == null) return ApiResponse<List<CallLogDto>>.Fail("Booking not found");

            var logs = await _callLogRepo.Query()
                .Where(cl => cl.BookingId == bookingId)
                .OrderByDescending(cl => cl.CallTime)
                .Select(cl => new CallLogDto
                {
                    Id = cl.Id,
                    CallTime = cl.CallTime,
                    Result = cl.Result,
                    Notes = cl.Notes,
                    StaffUserId = cl.StaffUserId
                }).ToListAsync();

            return ApiResponse<List<CallLogDto>>.Ok(logs);
        }
        catch (Exception ex)
        {
            return ApiResponse<List<CallLogDto>>.Fail($"Error retrieving call logs: {ex.Message}");
        }
    }

    public async Task<int> GetBookedRoomByDateAsync(Guid hotelId, DateTime date)
    {
        var bookings = await _bookingRepo.Query()
            .Where(x => x.HotelId == hotelId)
            .Where(x => x.StartDate <= date && date <= x.EndDate)
            .Where(x => x.Status != BookingStatus.Cancelled && x.Status != BookingStatus.Missing).ToListAsync();

        var sum = 0;
        foreach (var item in bookings)
        {
            sum += await _bookingRoomTypeRepo.Query().Where(x => x.BookingId == item.Id).Select(x => x.TotalRoom).SumAsync();
        }

        return sum;
    }

    public async Task<int> GetTotalRoomAsync(Guid hotelId)
    {
        return await _roomRepo.Query().CountAsync(x => x.HotelId == hotelId);
    }

    public async Task<ApiResponse<List<RoomMapItemDto>>> GetRoomMapAsync(RoomMapQueryDto query)
    {
        try
        {
            var targetDate = query.Date.Date;

            var roomsQuery = _roomRepo.Query().Include(r => r.RoomType).Where(r => true);
            if (query.HotelId.HasValue) roomsQuery = roomsQuery.Where(r => r.HotelId == query.HotelId.Value);
            var rooms = await roomsQuery.ToListAsync();

            var roomIds = rooms.Select(r => r.Id).ToList();
            var bookings = await _bookingRoomRepo.Query()
                .Where(br => roomIds.Contains(br.RoomId) && br.BookingStatus != BookingRoomStatus.Cancelled)
                .Where(br => targetDate < br.EndDate.Date && targetDate >= br.StartDate.Date)
                .Select(br => new { br.RoomId, br.StartDate, br.EndDate, br.BookingRoomId })
                .ToListAsync();
            var byRoom = bookings.GroupBy(b => b.RoomId).ToDictionary(g => g.Key, g => g.ToList());

            var result = rooms.Select(r => new RoomMapItemDto
            {
                RoomId = r.Id,
                RoomNumber = r.Number,
                RoomTypeId = r.RoomTypeId,
                RoomTypeName = r.RoomType?.Name ?? string.Empty,
                Floor = r.Floor,
                Status = r.Status,
                Timeline = BuildDayTimeline(targetDate, byRoom.TryGetValue(r.Id, out var list) && list.Any())
            }).ToList();

            return ApiResponse<List<RoomMapItemDto>>.Ok(result);
        }
        catch (Exception ex)
        {
            return ApiResponse<List<RoomMapItemDto>>.Fail($"Error building room map: {ex.Message}");
        }
    }

    public async Task<ApiResponse<object>> GetRoomAvailabilityAsync(RoomAvailabilityQueryDto query)
    {
        try
        {
            var q = _roomRepo.Query().Where(r => true);
            if (query.HotelId.HasValue) q = q.Where(r => r.HotelId == query.HotelId.Value);
            if (query.TypeId.HasValue) q = q.Where(r => r.RoomTypeId == query.TypeId.Value);
            var rooms = await q.Where(x => x.Status == RoomStatus.Available).ToListAsync();

            var from = query.From?.Date ?? DateTime.Now.Date;
            var to = query.To?.Date ?? from.AddDays(1);

            var roomIds = rooms.Select(r => r.Id).ToList();
            var assignedOverlaps = await _bookingRoomRepo.Query()
                .Where(br => roomIds.Contains(br.RoomId) && br.BookingStatus != BookingRoomStatus.Cancelled && br.BookingStatus != BookingRoomStatus.CheckedOut)
                .Where(br => from < br.EndDate && to > br.StartDate)
                .Select(br => br.RoomId)
                .Distinct()
                .CountAsync();

            var confirmedBookingIds = await _bookingRepo.Query()
                .Where(b => b.Status == BookingStatus.Confirmed)
                .Where(b => !query.HotelId.HasValue || b.HotelId == query.HotelId.Value)
                .Select(b => b.Id)
                .ToListAsync();

            var brts = await _bookingRoomTypeRepo.Query()
                .Where(rt => confirmedBookingIds.Contains(rt.BookingId))
                .Where(rt => !query.TypeId.HasValue || rt.RoomTypeId == query.TypeId.Value)
                .Where(rt => from < rt.EndDate && to > rt.StartDate)
                .Select(rt => new { rt.BookingRoomTypeId, rt.TotalRoom })
                .ToListAsync();

            var unassignedReserved = 0;
            foreach (var rt in brts)
            {
                var assignedForRt = await _bookingRoomRepo.Query()
                    .Where(br => br.BookingRoomTypeId == rt.BookingRoomTypeId)
                    .Where(br => from < br.EndDate && to > br.StartDate)
                    .CountAsync();
                var needed = Math.Max(rt.TotalRoom - assignedForRt, 0);
                unassignedReserved += needed;
            }

            var available = Math.Max(rooms.Count - assignedOverlaps - unassignedReserved, 0);
            var result = new { availableRooms = available, totalAvailable = available };

            return ApiResponse<object>.Ok(result);
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.Fail($"Error checking availability: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<BookingIntervalDto>>> GetRoomScheduleAsync(Guid roomId, DateTime from, DateTime to)
    {
        try
        {
            var baseItems = await _bookingRoomRepo.Query()
                .Where(br => br.RoomId == roomId && br.BookingStatus != BookingRoomStatus.Cancelled)
                .Where(br => from < br.EndDate && to > br.StartDate)
                .Select(br => new { br.BookingRoomTypeId, br.StartDate, br.EndDate })
                .OrderBy(x => x.StartDate)
                .ToListAsync();

            var intervals = new List<BookingIntervalDto>();
            foreach (var it in baseItems)
            {
                var brt = await _bookingRoomTypeRepo.Query()
                    .Where(x => x.BookingRoomTypeId == it.BookingRoomTypeId)
                    .Select(x => new { x.BookingId })
                    .FirstOrDefaultAsync();
                if (brt == null) continue;

                var booking = await _bookingRepo.Query()
                    .Where(b => b.Id == brt.BookingId)
                    .Select(b => new { b.Status, b.PrimaryGuestId })
                    .FirstOrDefaultAsync();
                if (booking == null) continue;

                string? guestName = null;
                if (booking.PrimaryGuestId.HasValue)
                {
                    guestName = await _guestRepo.Query()
                        .Where(g => g.Id == booking.PrimaryGuestId.Value)
                        .Select(g => g.FullName)
                        .FirstOrDefaultAsync();
                }

                intervals.Add(new BookingIntervalDto
                {
                    BookingId = brt.BookingId,
                    Start = it.StartDate,
                    End = it.EndDate,
                    Status = booking.Status,
                    GuestName = guestName
                });
            }

            return ApiResponse<List<BookingIntervalDto>>.Ok(intervals);
        }
        catch (Exception ex)
        {
            return ApiResponse<List<BookingIntervalDto>>.Fail($"Error retrieving room schedule: {ex.Message}");
        }
    }

    public async Task<ApiResponse<string>> GetCurrentBookingIdAsync(Guid roomId)
    {
        try
        {
            var now = DateTime.Now;
            var bookingRoom = await _bookingRoomRepo.Query()
                .Where(br => br.RoomId == roomId && br.BookingStatus != BookingRoomStatus.Cancelled)
                .Where(br => now < br.EndDate && now >= br.StartDate)
                .OrderByDescending(br => br.StartDate)
                .FirstOrDefaultAsync();

            if (bookingRoom == null)
            {
                return ApiResponse<string>.Fail("Không có booking hiện tại cho phòng này");
            }

            var bookingRoomType = await _bookingRoomTypeRepo.Query()
                .Where(x => x.BookingRoomTypeId == bookingRoom.BookingRoomTypeId)
                .FirstOrDefaultAsync();

            var bookingId = bookingRoomType?.BookingId;
            return ApiResponse<string>.Ok(bookingId.ToString());
        }
        catch (Exception ex)
        {
            return ApiResponse<string>.Fail($"Error retrieving current booking ID: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<RoomStayHistoryDto>>> GetRoomHistoryAsync(Guid roomId, DateTime? from, DateTime? to)
    {
        try
        {
            var baseQuery = _bookingRoomRepo.Query()
                .Where(br => br.RoomId == roomId && br.BookingStatus != BookingRoomStatus.Cancelled);

            if (from.HasValue && to.HasValue)
            {
                baseQuery = baseQuery.Where(br => from.Value < br.EndDate && to.Value > br.StartDate);
            }
            else if (from.HasValue)
            {
                baseQuery = baseQuery.Where(br => from.Value < br.EndDate);
            }
            else if (to.HasValue)
            {
                baseQuery = baseQuery.Where(br => to.Value > br.StartDate);
            }

            var baseItems = await baseQuery
                .Select(br => new { br.BookingRoomId, br.BookingRoomTypeId, br.StartDate, br.EndDate })
                .OrderBy(x => x.StartDate)
                .ToListAsync();

            var results = new List<RoomStayHistoryDto>();
            foreach (var it in baseItems)
            {
                var brt = await _bookingRoomTypeRepo.Query()
                    .Where(x => x.BookingRoomTypeId == it.BookingRoomTypeId)
                    .Select(x => new { x.BookingId })
                    .FirstOrDefaultAsync();
                if (brt == null) continue;

                var booking = await _bookingRepo.Query()
                    .Where(b => b.Id == brt.BookingId)
                    .Select(b => new { b.Status, b.PrimaryGuestId })
                    .FirstOrDefaultAsync();
                if (booking == null) continue;

                var guestIds = await _bookingGuestRepo.Query()
                    .Where(bg => bg.BookingRoomId == it.BookingRoomId)
                    .Select(bg => bg.GuestId)
                    .ToListAsync();

                var guests = new List<BookingGuestDto>();
                if (guestIds.Count > 0)
                {
                    guests = await _guestRepo.Query()
                        .Where(g => guestIds.Contains(g.Id))
                        .Select(g => new BookingGuestDto
                        {
                            GuestId = g.Id,
                            Fullname = g.FullName,
                            Phone = g.Phone,
                            Email = g.Email,
                            IdCard = g.IdCard,
                            IdCardFrontImageUrl = g.IdCardFrontImageUrl,
                            IdCardBackImageUrl = g.IdCardBackImageUrl
                        })
                        .ToListAsync();
                }

                string? primaryName = null;
                string? primaryPhone = null;
                if (booking.PrimaryGuestId.HasValue)
                {
                    primaryName = await _guestRepo.Query()
                        .Where(g => g.Id == booking.PrimaryGuestId.Value)
                        .Select(g => g.FullName)
                        .FirstOrDefaultAsync();

                    primaryPhone = await _guestRepo.Query()
                     .Where(g => g.Id == booking.PrimaryGuestId.Value)
                     .Select(g => g.Phone)
                     .FirstOrDefaultAsync();
                }

                results.Add(new RoomStayHistoryDto
                {
                    BookingId = brt.BookingId,
                    BookingRoomId = it.BookingRoomId,
                    Start = it.StartDate,
                    End = it.EndDate,
                    Status = booking.Status,
                    PrimaryGuestName = primaryName,
                    PrimaryGuestPhone = primaryPhone,
                    Guests = guests
                });
            }

            return ApiResponse<List<RoomStayHistoryDto>>.Ok(results);
        }
        catch (Exception ex)
        {
            return ApiResponse<List<RoomStayHistoryDto>>.Fail($"Error retrieving room history: {ex.Message}");
        }
    }

    private List<RoomTimelineSegmentDto> BuildDayTimeline(DateTime day, bool hasBooking)
    {
        var segments = new List<RoomTimelineSegmentDto>();
        var start = day.Date;
        var end = start.AddDays(1);
        if (hasBooking)
        {
            segments.Add(new RoomTimelineSegmentDto
            {
                Start = start,
                End = end,
                Status = RoomStatus.Occupied,
                BookingId = null
            });
        }
        else
        {
            segments.Add(new RoomTimelineSegmentDto
            {
                Start = start,
                End = end,
                Status = RoomStatus.Available,
                BookingId = null
            });
        }
        return segments;
    }

    public async Task<ApiResponse> AddRoomToBookingAsync(Guid bookingRoomTypeId, Guid roomId)
    {
        var bookingRoomType = await _bookingRoomTypeRepo.Query().Where(x => x.BookingRoomTypeId == bookingRoomTypeId).FirstOrDefaultAsync();

        if (bookingRoomType == null)
            return ApiResponse.Fail("Không tìm thấy booking");

        var room = await _roomRepo.Query().Where(x => x.Id == roomId).FirstOrDefaultAsync();

        if (room is not null)
        {
            room.Status = RoomStatus.Occupied;
            await _roomRepo.UpdateAsync(room);
            await _roomRepo.SaveChangesAsync();
        }

        await _bookingRoomRepo.AddAsync(new BookingRoom()
        {
            BookingRoomId = Guid.NewGuid(),
            BookingRoomTypeId = bookingRoomTypeId,
            RoomId = roomId,
            RoomName = room?.Number,
            StartDate = bookingRoomType.StartDate,
            EndDate = bookingRoomType.EndDate,
            BookingStatus = BookingRoomStatus.Pending
        });
        await _bookingRoomRepo.SaveChangesAsync();

        return ApiResponse.Ok("Thêm phòng thành công");
    }

    public async Task<ApiResponse> CheckInAsync(CheckInDto dto)
    {
        foreach (var guest in dto.Persons)
        {
            var exitsGuest = await _guestRepo.Query().Where(x => x.Phone == guest.Phone).FirstOrDefaultAsync();
            if (exitsGuest != null)
            {
                exitsGuest.FullName = guest.Name;
                exitsGuest.Phone = guest.Phone;
                exitsGuest.IdCard = guest.IdCard;
                exitsGuest.IdCardFrontImageUrl = guest.IdCardFrontImageUrl;
                exitsGuest.IdCardBackImageUrl = guest.IdCardBackImageUrl;
                await _guestRepo.UpdateAsync(exitsGuest);
                await _guestRepo.SaveChangesAsync();

                var bGuest = new BookingGuest()
                {

                    BookingRoomId = dto.RoomBookingId,
                    GuestId = exitsGuest.Id,
                };
                await _bookingGuestRepo.AddAsync(bGuest);
                await _bookingGuestRepo.SaveChangesAsync();
                continue;
            }

            var newGuest = new Guest()
            {
                HotelId = dto.HotelId,
                Id = Guid.NewGuid(),
                FullName = guest.Name,
                Phone = guest.Phone,
                IdCard = guest.IdCard,
                IdCardFrontImageUrl = guest.IdCardFrontImageUrl,
                IdCardBackImageUrl = guest.IdCardBackImageUrl
            };
            await _guestRepo.AddAsync(newGuest);
            await _guestRepo.SaveChangesAsync();

            var bookingGuest = new BookingGuest()
            {

                BookingRoomId = dto.RoomBookingId,
                GuestId = newGuest.Id,
            };
            await _bookingGuestRepo.AddAsync(bookingGuest);
            await _bookingGuestRepo.SaveChangesAsync();
        }

        var bookingRoom = await _bookingRoomRepo.FindAsync(dto.RoomBookingId);
        if (bookingRoom != null && bookingRoom.BookingStatus != BookingRoomStatus.CheckedIn)
        {
            bookingRoom.BookingStatus = BookingRoomStatus.CheckedIn;
            bookingRoom.ActualCheckInAt = dto.ActualCheckInAt ?? DateTime.Now;
            await _bookingRoomRepo.UpdateAsync(bookingRoom);
            await _bookingRoomRepo.SaveChangesAsync();

            var room = await _roomRepo.FindAsync(bookingRoom.RoomId);
            if (room != null)
            {
                room.Status = RoomStatus.Occupied;
                await _roomRepo.UpdateAsync(room);
                await _roomRepo.SaveChangesAsync();

                await _roomStatusLogRepo.AddAsync(new RoomStatusLog
                {
                    Id = Guid.NewGuid(),
                    HotelId = room.HotelId,
                    RoomId = room.Id,
                    Status = RoomStatus.Occupied,
                    Timestamp = DateTime.Now
                });
                await _roomStatusLogRepo.SaveChangesAsync();
            }
        }

        return ApiResponse.Ok("Check in thành công");
    }

    public async Task<ApiResponse> UpdateGuestInRoomAsync(Guid bookingRoomId, Guid guestId, UpdateGuestDto dto)
    {
        try
        {
            var bookingRoom = await _bookingRoomRepo.Query().Include(br => br.BookingRoomType).FirstOrDefaultAsync(br => br.BookingRoomId == bookingRoomId);
            if (bookingRoom == null) return ApiResponse.Fail("Không tìm thấy phòng trong booking");

            var bg = await _bookingGuestRepo.Query().FirstOrDefaultAsync(x => x.BookingRoomId == bookingRoomId && x.GuestId == guestId);
            if (bg == null) return ApiResponse.Fail("Không tìm thấy khách trong phòng");

            var guest = await _guestRepo.FindAsync(guestId);
            if (guest == null) return ApiResponse.Fail("Không tìm thấy khách");

            guest.FullName = dto.Fullname ?? guest.FullName;
            guest.Phone = dto.Phone ?? guest.Phone;
            guest.Email = dto.Email ?? guest.Email;
            guest.IdCardFrontImageUrl = dto.IdCardFrontImageUrl ?? guest.IdCardFrontImageUrl;
            guest.IdCardBackImageUrl = dto.IdCardBackImageUrl ?? guest.IdCardBackImageUrl;
            guest.IdCard = dto.IdCard ?? "";

            await _guestRepo.UpdateAsync(guest);
            await _guestRepo.SaveChangesAsync();

            return ApiResponse.Ok();
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail($"Error updating guest: {ex.Message}");
        }
    }

    public async Task<ApiResponse> RemoveGuestFromRoomAsync(Guid bookingRoomId, Guid guestId)
    {
        try
        {
            var bookingRoom = await _bookingRoomRepo.Query().Include(br => br.BookingRoomType).FirstOrDefaultAsync(br => br.BookingRoomId == bookingRoomId);
            if (bookingRoom == null) return ApiResponse.Fail("Không tìm thấy phòng trong booking");

            var bg = await _bookingGuestRepo.Query().FirstOrDefaultAsync(x => x.BookingRoomId == bookingRoomId && x.GuestId == guestId);
            if (bg == null) return ApiResponse.Fail("Không tìm thấy khách trong phòng");

            await _bookingGuestRepo.RemoveAsync(bg);
            await _bookingGuestRepo.SaveChangesAsync();

            return ApiResponse.Ok();
        }
        catch (Exception ex)
        {
            return ApiResponse.Fail($"Error removing guest: {ex.Message}");
        }
    }

    public async Task<ApiResponse<BookingDetailsDto>> UpdateRoomDatesAsync(Guid bookingRoomId, DateTime startDate, DateTime endDate)
    {
        var bookingRoom = await _bookingRoomRepo.Query().Include(br => br.BookingRoomType).FirstOrDefaultAsync(br => br.BookingRoomId == bookingRoomId);
        if (bookingRoom == null) return ApiResponse<BookingDetailsDto>.Fail("Không tìm thấy phòng trong booking");
        if (endDate <= startDate) return ApiResponse<BookingDetailsDto>.Fail("Khoảng thời gian không hợp lệ");

        var typeStart = bookingRoom.BookingRoomType!.StartDate;
        var typeEnd = bookingRoom.BookingRoomType!.EndDate;
        if (startDate < typeStart || endDate > typeEnd) return ApiResponse<BookingDetailsDto>.Fail("Thời gian nằm ngoài khoảng của loại phòng");

        var hasOverlap = await _bookingRoomRepo.Query()
            .Where(br => br.RoomId == bookingRoom.RoomId && br.BookingRoomId != bookingRoomId && br.BookingStatus != BookingRoomStatus.Cancelled)
            .AnyAsync(br => startDate < br.EndDate && endDate > br.StartDate);
        if (hasOverlap) return ApiResponse<BookingDetailsDto>.Fail("Trùng lịch với booking khác");

        bookingRoom.StartDate = startDate;
        bookingRoom.EndDate = endDate;
        await _bookingRoomRepo.UpdateAsync(bookingRoom);
        await _bookingRoomRepo.SaveChangesAsync();

        var bookingId = bookingRoom.BookingRoomType!.BookingId;
        return await GetByIdAsync(bookingId);
    }

    public async Task<ApiResponse<BookingDetailsDto>> UpdateRoomActualTimesAsync(Guid bookingRoomId, DateTime? actualCheckInAt, DateTime? actualCheckOutAt)
    {
        var bookingRoom = await _bookingRoomRepo.Query().Include(br => br.BookingRoomType).FirstOrDefaultAsync(br => br.BookingRoomId == bookingRoomId);
        if (bookingRoom == null) return ApiResponse<BookingDetailsDto>.Fail("Không tìm thấy phòng trong booking");

        if (actualCheckInAt.HasValue && actualCheckOutAt.HasValue && actualCheckOutAt.Value <= actualCheckInAt.Value)
            return ApiResponse<BookingDetailsDto>.Fail("Check-out phải sau Check-in");

        var start = bookingRoom.StartDate;
        var end = bookingRoom.EndDate;
        if (actualCheckInAt.HasValue && (actualCheckInAt.Value < start || actualCheckInAt.Value > end))
            return ApiResponse<BookingDetailsDto>.Fail("Thời gian check-in nằm ngoài khoảng dự kiến");
        if (actualCheckOutAt.HasValue && (actualCheckOutAt.Value < start))
            return ApiResponse<BookingDetailsDto>.Fail("Thời gian check-out nằm ngoài khoảng dự kiến");

        if (actualCheckInAt.HasValue)
        {
            bookingRoom.ActualCheckInAt = actualCheckInAt.Value;
            bookingRoom.BookingStatus = BookingRoomStatus.CheckedIn;
            var room = await _roomRepo.FindAsync(bookingRoom.RoomId);
            if (room != null)
            {
                room.Status = RoomStatus.Occupied;
                await _roomRepo.UpdateAsync(room);
                await _roomRepo.SaveChangesAsync();

                await _roomStatusLogRepo.AddAsync(new RoomStatusLog
                {
                    Id = Guid.NewGuid(),
                    HotelId = room.HotelId,
                    RoomId = room.Id,
                    Status = RoomStatus.Occupied,
                    Timestamp = actualCheckInAt.Value
                });
                await _roomStatusLogRepo.SaveChangesAsync();
            }
        }

        if (actualCheckOutAt.HasValue)
        {
            bookingRoom.ActualCheckOutAt = actualCheckOutAt.Value;
            bookingRoom.BookingStatus = BookingRoomStatus.CheckedOut;
            var room = await _roomRepo.FindAsync(bookingRoom.RoomId);
            if (room != null)
            {
                room.Status = RoomStatus.Dirty;
                await _roomRepo.UpdateAsync(room);
                await _roomRepo.SaveChangesAsync();

                await _roomStatusLogRepo.AddAsync(new RoomStatusLog
                {
                    Id = Guid.NewGuid(),
                    HotelId = room.HotelId,
                    RoomId = room.Id,
                    Status = RoomStatus.Dirty,
                    Timestamp = actualCheckOutAt.Value
                });
                await _roomStatusLogRepo.SaveChangesAsync();
            }
        }

        await _bookingRoomRepo.UpdateAsync(bookingRoom);
        await _bookingRoomRepo.SaveChangesAsync();

        var bookingRoomType = await _bookingRoomTypeRepo.Query()
            .Where(x => x.BookingRoomTypeId == bookingRoom.BookingRoomTypeId)
            .FirstOrDefaultAsync();

        return await GetByIdAsync(bookingRoomType!.BookingId);
    }

    public async Task<ApiResponse<BookingDetailsDto>> MoveGuestAsync(Guid bookingRoomId, Guid guestId, Guid targetBookingRoomId)
    {
        var fromRoom = await _bookingRoomRepo.Query().Include(br => br.BookingRoomType).FirstOrDefaultAsync(br => br.BookingRoomId == bookingRoomId);
        if (fromRoom == null) return ApiResponse<BookingDetailsDto>.Fail("Không tìm thấy phòng nguồn");
        var toRoom = await _bookingRoomRepo.Query().Include(br => br.BookingRoomType).FirstOrDefaultAsync(br => br.BookingRoomId == targetBookingRoomId);
        if (toRoom == null) return ApiResponse<BookingDetailsDto>.Fail("Không tìm thấy phòng đích");

        if (fromRoom.BookingRoomType!.BookingId != toRoom.BookingRoomType!.BookingId)
            return ApiResponse<BookingDetailsDto>.Fail("Phòng đích phải thuộc cùng booking");

        var bg = await _bookingGuestRepo.Query().FirstOrDefaultAsync(x => x.BookingRoomId == bookingRoomId && x.GuestId == guestId);
        if (bg == null) return ApiResponse<BookingDetailsDto>.Fail("Không tìm thấy khách trong phòng nguồn");

        bg.BookingRoomId = targetBookingRoomId;
        await _bookingGuestRepo.UpdateAsync(bg);
        await _bookingGuestRepo.SaveChangesAsync();

        var bookingId = fromRoom.BookingRoomType!.BookingId;
        return await GetByIdAsync(bookingId);
    }

    public async Task<ApiResponse<BookingDetailsDto>> SwapGuestsAsync(Guid bookingRoomId, Guid guestId, Guid targetBookingRoomId, Guid targetGuestId)
    {
        var fromRoom = await _bookingRoomRepo.Query().Include(br => br.BookingRoomType).FirstOrDefaultAsync(br => br.BookingRoomId == bookingRoomId);
        if (fromRoom == null) return ApiResponse<BookingDetailsDto>.Fail("Không tìm thấy phòng nguồn");
        var toRoom = await _bookingRoomRepo.Query().Include(br => br.BookingRoomType).FirstOrDefaultAsync(br => br.BookingRoomId == targetBookingRoomId);
        if (toRoom == null) return ApiResponse<BookingDetailsDto>.Fail("Không tìm thấy phòng đích");

        var bookingRoomTypeFrom = await _bookingRoomTypeRepo.Query()
            .Where(x => x.BookingRoomTypeId == fromRoom.BookingRoomTypeId)
            .FirstOrDefaultAsync();

        var bookingRoomTypeTo = await _bookingRoomTypeRepo.Query()
            .Where(x => x.BookingRoomTypeId == toRoom.BookingRoomTypeId)
            .FirstOrDefaultAsync();

        if (bookingRoomTypeFrom!.BookingId != bookingRoomTypeTo!.BookingId)
            return ApiResponse<BookingDetailsDto>.Fail("Phòng đích phải thuộc cùng booking");
        if (fromRoom.BookingRoomTypeId != toRoom.BookingRoomTypeId)
            return ApiResponse<BookingDetailsDto>.Fail("Phòng đích phải thuộc cùng loại phòng");

        var src = await _bookingGuestRepo.Query().FirstOrDefaultAsync(x => x.BookingRoomId == bookingRoomId && x.GuestId == guestId);
        if (src == null) return ApiResponse<BookingDetailsDto>.Fail("Không tìm thấy khách trong phòng nguồn");
        var dst = await _bookingGuestRepo.Query().FirstOrDefaultAsync(x => x.BookingRoomId == targetBookingRoomId && x.GuestId == targetGuestId);
        if (dst == null) return ApiResponse<BookingDetailsDto>.Fail("Không tìm thấy khách trong phòng đích");

        src.BookingRoomId = targetBookingRoomId;
        await _bookingGuestRepo.UpdateAsync(src);
        await _bookingGuestRepo.SaveChangesAsync();

        dst.BookingRoomId = bookingRoomId;
        await _bookingGuestRepo.UpdateAsync(dst);
        await _bookingGuestRepo.SaveChangesAsync();

        var bookingId = bookingRoomTypeFrom!.BookingId;
        return await GetByIdAsync(bookingId);
    }

    public async Task<ApiResponse<BookingDetailsDto>> ChangeRoomAsync(Guid bookingRoomId, Guid newRoomId)
    {
        var bookingRoom = await _bookingRoomRepo.Query().Include(br => br.BookingRoomType).FirstOrDefaultAsync(br => br.BookingRoomId == bookingRoomId);
        if (bookingRoom == null) return ApiResponse<BookingDetailsDto>.Fail("Không tìm thấy booking");

        var bookingRoomType = await _bookingRoomTypeRepo.Query()
            .Where(x => x.BookingRoomTypeId == bookingRoom.BookingRoomTypeId)
            .FirstOrDefaultAsync();

        var booking = await _bookingRepo.FindAsync(bookingRoomType!.BookingId);
        if (booking == null) return ApiResponse<BookingDetailsDto>.Fail("Không tìm thấy booking");

        var targetRoom = await _roomRepo.FindAsync(newRoomId);
        if (targetRoom == null || targetRoom.HotelId != booking.HotelId) return ApiResponse<BookingDetailsDto>.Fail("Phòng không hợp lệ");


        var q = _roomRepo.Query().Include(r => r.RoomType).Where(r => true);
        q = q.Where(r => r.RoomTypeId == bookingRoom.BookingRoomTypeId);
        var rooms = await q.ToListAsync();


        var from = bookingRoom?.StartDate ?? DateTime.Now.Date;
        var to = bookingRoom?.EndDate ?? from.AddDays(1);

        var roomIds = rooms.Select(r => r.Id).ToList();
        var overlapping = await _bookingRoomRepo.Query()
             .Where(br => roomIds.Contains(br.RoomId) && br.BookingStatus != BookingRoomStatus.Cancelled)
             .Where(br => from < br.EndDate && to > br.StartDate)
             .Select(br => br.RoomId)
             .Distinct()
             .ToListAsync();

        var unavailable = overlapping.Contains(newRoomId);

        //var overlap = await _bookingRoomRepo.Query()
        //    .Where(br => br.RoomId == newRoomId && br.BookingStatus != BookingRoomStatus.Cancelled && br.BookingStatus != BookingRoomStatus.CheckedOut
        //       && br.BookingRoomId != bookingRoomId)
        //    .AnyAsync(br => bookingRoom.StartDate < br.EndDate && bookingRoom.EndDate > br.StartDate);
        if (unavailable) return ApiResponse<BookingDetailsDto>.Fail("Phòng không trống trong khoảng thời gian");

        var oldRoom = await _roomRepo.FindAsync(bookingRoom.RoomId);

        bookingRoom.RoomId = newRoomId;
        bookingRoom.RoomName = targetRoom.Number;
        await _bookingRoomRepo.UpdateAsync(bookingRoom);
        await _bookingRoomRepo.SaveChangesAsync();

        if (oldRoom != null)
        {
            oldRoom.Status = RoomStatus.Available;
            await _roomRepo.UpdateAsync(oldRoom);
            await _roomRepo.SaveChangesAsync();

            await _roomStatusLogRepo.AddAsync(new RoomStatusLog
            {
                Id = Guid.NewGuid(),
                HotelId = oldRoom.HotelId,
                RoomId = oldRoom.Id,
                Status = RoomStatus.Available,
                Timestamp = DateTime.Now
            });
            await _roomStatusLogRepo.SaveChangesAsync();
        }

        targetRoom.Status = bookingRoom.BookingStatus == BookingRoomStatus.CheckedIn ? RoomStatus.Occupied : targetRoom.Status;
        await _roomRepo.UpdateAsync(targetRoom);
        await _roomRepo.SaveChangesAsync();

        await _roomStatusLogRepo.AddAsync(new RoomStatusLog
        {
            Id = Guid.NewGuid(),
            HotelId = targetRoom.HotelId,
            RoomId = targetRoom.Id,
            Status = targetRoom.Status,
            Timestamp = DateTime.Now
        });
        await _roomStatusLogRepo.SaveChangesAsync();

        return await GetByIdAsync(booking.Id);
    }

    public async Task<ApiResponse> ExtendStayAsync(Guid bookingRoomId, DateTime newEndDate, string? discountCode)
    {
        var bookingRoom = await _bookingRoomRepo.Query()
            .Include(br => br.BookingRoomType).FirstOrDefaultAsync(br => br.BookingRoomId == bookingRoomId);
        if (bookingRoom == null) return ApiResponse.Fail("Không tìm thấy booking");

        if (newEndDate.Date <= bookingRoom.EndDate.Date) return ApiResponse.Fail("Ngày kết thúc không hợp lệ");

        var overlap = await _bookingRoomRepo.Query()
            .Where(br => br.RoomId == bookingRoom.RoomId && br.BookingStatus != BookingRoomStatus.Cancelled && br.BookingRoomId != bookingRoomId)
            .AnyAsync(br => bookingRoom.EndDate < br.EndDate && newEndDate > br.StartDate);
        if (overlap) return ApiResponse.Fail("Không thể gia hạn do trùng lịch");

        var bookingRoomType = await _bookingRoomTypeRepo.Query()
            .Where(x => x.BookingRoomTypeId == bookingRoom.BookingRoomTypeId)
            .FirstOrDefaultAsync();

        var nights = (newEndDate.Date - bookingRoom.EndDate.Date).Days;
        var pricePerNight = bookingRoomType?.Price;
        var delta = pricePerNight * nights;

        bookingRoom.ExtendedDate = newEndDate;
        //bookingRoom.EndDate = newEndDate;
        await _bookingRoomRepo.UpdateAsync(bookingRoom);
        await _bookingRoomRepo.SaveChangesAsync();

        var booking = await _bookingRepo.FindAsync(bookingRoomType!.BookingId);
        if (booking != null)
        {
            booking.TotalAmount += delta ?? 0;
            booking.LeftAmount += delta ?? 0;
            await _bookingRepo.UpdateAsync(booking);
            await _bookingRepo.SaveChangesAsync();
        }

        return ApiResponse.Ok();
    }

    public async Task<ApiResponse<CheckoutResultDto>> CheckOutAsync(Guid bookingId, CheckoutRequestDto dto)
    {
        try
        {
            var booking = await _bookingRepo.Query()
                .Include(b => b.BookingRoomTypes)
                .ThenInclude(rt => rt.BookingRooms)
                .Include(b => b.BookingRoomTypes)
                .ThenInclude(rt => rt.RoomType)
                .FirstOrDefaultAsync(b => b.Id == bookingId);
            if (booking == null) return ApiResponse<CheckoutResultDto>.Fail("Không tìm thấy booking");

            var totalPaid = booking.DepositAmount + (dto.FinalPayment?.Amount ?? 0);
            foreach (var br in booking.BookingRoomTypes.SelectMany(rt => rt.BookingRooms))
            {
                br.BookingStatus = BookingRoomStatus.CheckedOut;
                br.ActualCheckOutAt = dto.CheckoutTime ?? DateTime.Now;
                await _bookingRoomRepo.UpdateAsync(br);
            }
            await _bookingRoomRepo.SaveChangesAsync();



            decimal baseTotal = 0;

            var bookingRoomTypes = await _bookingRoomTypeRepo.Query().Where(x => x.BookingId == booking.Id).ToListAsync();

            foreach (var rt in bookingRoomTypes)
            {
                var bookingRooms = await _bookingRoomRepo.Query().Where(x => x.BookingRoomTypeId == rt.BookingRoomTypeId).ToListAsync();
                var roomType = rt.RoomType ?? await _roomTypeRepo.FindAsync(rt.RoomTypeId);
                var overrides = ParseOverrides(roomType?.Prices);
                foreach (var br in bookingRooms)
                {
                    var start = (br.ActualCheckInAt?.Date ?? br.StartDate.Date);
                    var endExclusive = (br.ActualCheckOutAt?.Date ?? br.EndDate.Date);
                    if (endExclusive <= start) endExclusive = start.AddDays(1);
                    for (var d = start; d < endExclusive; d = d.AddDays(1))
                    {
                        var match = overrides.FirstOrDefault(p => p.Date.Date == d);
                        var price = match?.Price ?? (roomType?.BasePriceFrom ?? rt.Price);
                        baseTotal += price;
                    }
                }
            }


            //booking.Status = BookingStatus.Completed;
            booking.AdditionalNotes = dto.AdditionalNotes;
            booking.AdditionalAmount = dto.AdditionalAmount ?? 0;
            booking.AdditionalBookingNotes = dto.AdditionalBookingNotes;
            booking.AdditionalBookingAmount = dto.AdditionalBookingAmount ?? 0;
            booking.TotalAmount = baseTotal + booking.AdditionalAmount ;
            booking.LeftAmount = Math.Max(0, booking.TotalAmount - totalPaid);

            await _bookingRepo.UpdateAsync(booking);
            await _bookingRepo.SaveChangesAsync();

            var rooms = booking.BookingRoomTypes.SelectMany(rt => rt.BookingRooms).Select(r => r.RoomId).Distinct().ToList();
            foreach (var roomId in rooms)
            {
                var room = await _roomRepo.FindAsync(roomId);
                if (room != null)
                {
                    room.Status = RoomStatus.Dirty;
                    await _roomRepo.UpdateAsync(room);
                    await _roomRepo.SaveChangesAsync();

                    await _roomStatusLogRepo.AddAsync(new RoomStatusLog
                    {
                        Id = Guid.NewGuid(),
                        HotelId = room.HotelId,
                        RoomId = room.Id,
                        Status = RoomStatus.Dirty,
                        Timestamp = dto.CheckoutTime ?? DateTime.Now
                    });
                    await _roomStatusLogRepo.SaveChangesAsync();
                }
            }

            var details = await GetByIdAsync(bookingId);
            if (!details.IsSuccess) return ApiResponse<CheckoutResultDto>.Fail(details.Message ?? "");

            return ApiResponse<CheckoutResultDto>.Ok(new CheckoutResultDto { TotalPaid = totalPaid, Booking = details.Data, CheckoutTime = dto.CheckoutTime ?? DateTime.Now });
        }
        catch (Exception ex)
        {

            return ApiResponse<CheckoutResultDto>.Fail(ex.Message);
        }
    }


    public async Task<ApiResponse<CheckoutResultDto>> AddBookingInvoiceAsync(Guid bookingId, CheckoutRequestDto dto)
    {
        try
        {
            var booking = await _bookingRepo.Query().Include(b => b.BookingRoomTypes).ThenInclude(rt => rt.BookingRooms).FirstOrDefaultAsync(b => b.Id == bookingId);
            if (booking == null) return ApiResponse<CheckoutResultDto>.Fail("Không tìm thấy booking");

            var lines = new List<InvoiceLine>();

            foreach (var rt in booking.BookingRoomTypes)
            {
                var totalNights = (rt.EndDate.Date - rt.StartDate.Date).Days;
                var amount = rt.Price * totalNights * Math.Max(rt.BookingRooms.Count, 1);
                lines.Add(new InvoiceLine
                {
                    Id = Guid.NewGuid(),
                    Description = rt.RoomTypeName ?? "Room charge",
                    Amount = amount,
                    SourceType = InvoiceLineSourceType.RoomCharge,
                    SourceId = rt.BookingRoomTypeId
                });
            }

            var rules = await _surchargeRuleRepo.Query().Where(x => x.HotelId == booking.HotelId).ToListAsync();

            //if (booking.DepositAmount > 0)
            //{
            //    lines.Add(new InvoiceLine
            //    {
            //        Id = Guid.NewGuid(),
            //        Description = "Deposit deduction",
            //        Amount = -booking.DepositAmount,
            //        SourceType = InvoiceLineSourceType.Discount
            //    });
            //}

            if (!string.IsNullOrWhiteSpace(dto.DiscountCode))
            {
                var now = DateTime.Now;
                var promo = await _promotionRepo.Query()
                    .FirstOrDefaultAsync(p => p.HotelId == booking.HotelId && p.Code == dto.DiscountCode && p.IsActive && p.StartDate <= now && p.EndDate >= now);
                if (promo == null)
                {
                    return ApiResponse<CheckoutResultDto>.Fail("Mã giảm giá không hợp lệ hoặc hết hạn");
                }
                var scope = promo.Scope;
                if (!string.Equals(scope, "booking", StringComparison.OrdinalIgnoreCase))
                {
                    return ApiResponse<CheckoutResultDto>.Fail("Mã giảm giá chỉ áp dụng cho đặt phòng");
                }
                var baseAmount = lines.Where(l => l.Amount > 0).Sum(l => l.Amount);
                var discountAmt = Math.Round(baseAmount * promo.Value / 100m, 2);
                if (discountAmt > 0)
                {
                    lines.Add(new InvoiceLine
                    {
                        Id = Guid.NewGuid(),
                        Description = $"Discount code {promo.Code}",
                        Amount = -discountAmt,
                        SourceType = InvoiceLineSourceType.Discount,
                        SourceId = promo.Id
                    });
                }

                booking.PromotionCode = promo.Code;
                booking.PromotionValue = promo.Value;
                booking.DiscountAmount = discountAmt;
            }
            else
            {
                booking.PromotionCode = null;
                booking.PromotionValue = 0;
            }

            if (dto.AdditionalAmount > 0)
            {
                lines.Add(new InvoiceLine
                {
                    Id = Guid.NewGuid(),
                    Description = $"Phụ thu",
                    Amount = dto.AdditionalAmount ?? 0,
                    SourceType = InvoiceLineSourceType.Surcharge,
                });
            }

            if (dto.AdditionalBookingAmount > 0)
            {
                lines.Add(new InvoiceLine
                {
                    Id = Guid.NewGuid(),
                    Description = $"Phụ thu",
                    Amount = dto.AdditionalBookingAmount ?? 0,
                    SourceType = InvoiceLineSourceType.Surcharge,
                });
            }

            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                HotelId = booking.HotelId,
                BookingId = booking.Id,
                InvoiceNumber = $"INV-{DateTime.Now:yyMM}-{new Random().Next(100000, 999999)}",
                Status = InvoiceStatus.Draft,
                CreatedById = Guid.Empty,
                CreatedAt = DateTime.Now,
                VatIncluded = true,
                Notes = dto.Notes,
                AdditionalAmount = dto.AdditionalAmount
            };

            foreach (var l in lines)
            {
                l.InvoiceId = invoice.Id;
                invoice.Lines.Add(l);
            }



            invoice.SubTotal = invoice.Lines.Where(x => x.Amount > 0).Sum(x => x.Amount);
            invoice.DiscountAmount = Math.Abs(invoice.Lines.Where(x => x.Amount < 0).Sum(x => x.Amount));
            invoice.TaxAmount = Math.Round(invoice.SubTotal * 0.1m, 2);
            invoice.TotalAmount = booking.TotalAmount;

            await _invoiceRepo.AddAsync(invoice);
            await _invoiceRepo.SaveChangesAsync();

            foreach (var br in booking.BookingRoomTypes.SelectMany(rt => rt.BookingRooms))
            {
                br.BookingStatus = BookingRoomStatus.CheckedOut;
                br.ActualCheckOutAt = dto.CheckoutTime ?? DateTime.Now;
                await _bookingRoomRepo.UpdateAsync(br);
            }
            await _bookingRoomRepo.SaveChangesAsync();

            //booking.Status = BookingStatus.Completed;
            booking.AdditionalNotes = dto.AdditionalNotes;
            booking.AdditionalAmount = dto.AdditionalAmount ?? 0;
            booking.AdditionalBookingNotes = dto.AdditionalBookingNotes;
            booking.AdditionalBookingAmount = dto.AdditionalBookingAmount ?? 0;

            await _bookingRepo.UpdateAsync(booking);
            await _bookingRepo.SaveChangesAsync();

            var rooms = booking.BookingRoomTypes.SelectMany(rt => rt.BookingRooms).Select(r => r.RoomId).Distinct().ToList();
            foreach (var roomId in rooms)
            {
                var room = await _roomRepo.FindAsync(roomId);
                if (room != null)
                {
                    room.Status = RoomStatus.Dirty;
                    await _roomRepo.UpdateAsync(room);
                    await _roomRepo.SaveChangesAsync();

                    await _roomStatusLogRepo.AddAsync(new RoomStatusLog
                    {
                        Id = Guid.NewGuid(),
                        HotelId = room.HotelId,
                        RoomId = room.Id,
                        Status = RoomStatus.Dirty,
                        Timestamp = dto.CheckoutTime ?? DateTime.Now
                    });
                    await _roomStatusLogRepo.SaveChangesAsync();
                }
            }

            var details = await GetByIdAsync(bookingId);
            if (!details.IsSuccess) return ApiResponse<CheckoutResultDto>.Fail(details.Message ?? "");

            return ApiResponse<CheckoutResultDto>.Ok(new CheckoutResultDto { TotalPaid = 0, Booking = details.Data, CheckoutTime = dto.CheckoutTime ?? DateTime.Now });
        }
        catch (Exception ex)
        {

            return ApiResponse<CheckoutResultDto>.Fail(ex.Message);
        }
    }



    public async Task<ApiResponse<AdditionalChargesDto>> GetAdditionalChargesPreviewAsync(Guid bookingId)
    {
        var booking = await _bookingRepo.Query().Include(b => b.BookingRoomTypes).ThenInclude(rt => rt.BookingRooms).FirstOrDefaultAsync(b => b.Id == bookingId);
        if (booking == null) return ApiResponse<AdditionalChargesDto>.Fail("Không tìm thấy booking");

        var rules = await _surchargeRuleRepo.Query().Where(x => x.HotelId == booking.HotelId).ToListAsync();
        var lines = new List<AdditionalChargeLineDto>();

        var earlyRule = rules.FirstOrDefault(r => r.Type == SurchargeType.EarlyCheckIn);
        if (earlyRule != null)
        {
            var amt = earlyRule.IsPercentage ? 0 : earlyRule.Amount;
            lines.Add(new AdditionalChargeLineDto { Description = "Early check-in", Amount = amt, SourceType = InvoiceLineSourceType.Surcharge });
        }

        var lateRule = rules.FirstOrDefault(r => r.Type == SurchargeType.LateCheckOut);
        if (lateRule != null)
        {
            var amt = lateRule.IsPercentage ? 0 : lateRule.Amount;
            lines.Add(new AdditionalChargeLineDto { Description = "Late check-out", Amount = amt, SourceType = InvoiceLineSourceType.Surcharge });
        }

        var capacityTotal = booking.BookingRoomTypes.Sum(rt => rt.Capacity * Math.Max(rt.BookingRooms.Count, 1));
        var guestCount = await _bookingGuestRepo.Query().Where(bg => booking.BookingRoomTypes.SelectMany(rt => rt.BookingRooms).Select(r => r.BookingRoomId).Contains(bg.BookingRoomId)).CountAsync();
        var extraGuests = Math.Max(guestCount - capacityTotal, 0);
        var extraRule = rules.FirstOrDefault(r => r.Type == SurchargeType.ExtraGuest);
        if (extraRule != null && extraGuests > 0)
        {
            var amt = extraRule.IsPercentage ? 0 : extraRule.Amount * extraGuests;
            lines.Add(new AdditionalChargeLineDto { Description = "Extra guests", Amount = amt, SourceType = InvoiceLineSourceType.Surcharge });
        }

        var total = lines.Sum(l => l.Amount);
        return ApiResponse<AdditionalChargesDto>.Ok(new AdditionalChargesDto { Lines = lines, Total = total });
    }

    public async Task<ApiResponse> RecordMinibarConsumptionAsync(Guid bookingId, MinibarConsumptionDto dto)
    {

        try
        {
            foreach (var item in dto.Items)
            {
                var minibar = await _minibarRepo.FindAsync(item.MinibarId);
                if (minibar == null) continue;

                var consumed = Math.Max(0, item.Quantity);

                var mb = new MinibarBooking
                {
                    Id = Guid.NewGuid(),
                    HouseKeepingTaskId = bookingId,
                    MinibarId = item.MinibarId,
                    MinibarName = minibar.Name,
                    MinibarPrice = minibar.Price,
                    ComsumedQuantity = consumed,
                    OriginalQuantity = minibar.Quantity,
                    MinibarBookingStatus = consumed == minibar.Quantity ? MinibarBookingStatus.Full : MinibarBookingStatus.Missing
                };
                await _minibarBookingRepo.AddAsync(mb);
                await _minibarBookingRepo.SaveChangesAsync();
            }

            return ApiResponse.Ok("Đã ghi nhận minibar");
        }
        catch (Exception ex)
        {

            return ApiResponse.Fail(ex.Message);

        }
    }

    public async Task<ApiResponse<List<PeakDayDto>>> GetPeakDaysAsync(PeakDaysQueryDto query)
    {
        if (query.HotelId == Guid.Empty) return ApiResponse<List<PeakDayDto>>.Fail("HotelId is required");
        var from = query.From.Date;
        var to = query.To.Date;
        if (to < from) return ApiResponse<List<PeakDayDto>>.Fail("Invalid date range");
        var rooms = await _roomRepo.Query().Where(r => r.HotelId == query.HotelId).Select(r => r.Id).ToListAsync();
        var totalRooms = rooms.Count;
        if (totalRooms == 0) return ApiResponse<List<PeakDayDto>>.Ok([]);
        var result = new List<PeakDayDto>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var bookedRoomIds = await _bookingRoomRepo.Query()
                .Where(br => rooms.Contains(br.RoomId) && br.BookingStatus != BookingRoomStatus.Cancelled)
                .Where(br => d < br.EndDate.Date && d >= br.StartDate.Date)
                .Select(br => br.RoomId)
                .Distinct()
                .ToListAsync();
            var booked = bookedRoomIds.Count;
            var pct = totalRooms == 0 ? 0 : (double)booked / totalRooms * 100.0;
            if (pct >= 75.0)
            {
                result.Add(new PeakDayDto { Date = d, TotalRooms = totalRooms, BookedRooms = booked, Percentage = Math.Round(pct, 2) });
            }
        }
        return ApiResponse<List<PeakDayDto>>.Ok(result);
    }

    public async Task<ApiResponse<NoShowCancelResultDto>> CancelNoShowsAsync(NoShowCancelRequestDto request)
    {
        var targetDate = (request.Date ?? DateTime.Now.Date).Date;
        var roomsQuery = _bookingRoomRepo.Query()
            .Include(br => br.BookingRoomType)
            .Include(br => br.HotelRoom)
            .Where(br => br.BookingStatus == BookingRoomStatus.Pending && br.ActualCheckInAt == null)
            .Where(br => br.StartDate.Date == targetDate);
        if (request.HotelId.HasValue)
        {
            roomsQuery = roomsQuery.Where(br => br.HotelRoom != null && br.HotelRoom.HotelId == request.HotelId.Value);
        }
        var rooms = await roomsQuery.ToListAsync();
        var cancelledRooms = 0;
        var affectedBookingIds = new HashSet<Guid>();
        foreach (var br in rooms)
        {
            br.BookingStatus = BookingRoomStatus.Cancelled;
            await _bookingRoomRepo.UpdateAsync(br);
            cancelledRooms++;
            var room = await _roomRepo.FindAsync(br.RoomId);
            if (room != null)
            {
                room.Status = RoomStatus.Available;
                await _roomRepo.UpdateAsync(room);
            }
            var bid = br.BookingRoomType?.BookingId ?? Guid.Empty;
            if (bid != Guid.Empty) affectedBookingIds.Add(bid);
        }
        await _roomRepo.SaveChangesAsync();
        await _bookingRoomRepo.SaveChangesAsync();
        foreach (var bid in affectedBookingIds)
        {
            var booking = await _bookingRepo.Query().Include(b => b.BookingRoomTypes).ThenInclude(rt => rt.BookingRooms).FirstOrDefaultAsync(b => b.Id == bid);
            if (booking == null) continue;
            var allCancelled = booking.BookingRoomTypes.SelectMany(rt => rt.BookingRooms).All(r => r.BookingStatus == BookingRoomStatus.Cancelled);
            if (allCancelled)
            {
                booking.Status = BookingStatus.Cancelled;
                await _bookingRepo.UpdateAsync(booking);
            }
        }
        await _bookingRepo.SaveChangesAsync();
        return ApiResponse<NoShowCancelResultDto>.Ok(new NoShowCancelResultDto { CancelledRooms = cancelledRooms, AffectedBookings = affectedBookingIds.Count });
    }

    public async Task<ApiResponse<EarlyCheckoutFeeResponseDto>> CalculateEarlyCheckoutFeeAsync(Guid bookingId, EarlyCheckoutFeeRequestDto dto)
    {
        var booking = await _bookingRepo.Query().Include(b => b.BookingRoomTypes).ThenInclude(rt => rt.BookingRooms).FirstOrDefaultAsync(b => b.Id == bookingId);
        if (booking == null) return ApiResponse<EarlyCheckoutFeeResponseDto>.Fail("Không tìm thấy booking");
        var hotelId = booking.HotelId;
        var allRooms = await _roomRepo.Query().Where(r => r.HotelId == hotelId).Select(r => r.Id).ToListAsync();
        var totalRooms = allRooms.Count;
        if (totalRooms == 0) return ApiResponse<EarlyCheckoutFeeResponseDto>.Ok(new EarlyCheckoutFeeResponseDto { AvailabilityPercent = 100, Tier = "81-100", FeePercentage = 0, FeeAmount = 0 });
        var d = dto.CheckoutDate.Date;
        var overlappingRoomIds = await _bookingRoomRepo.Query()
            .Where(br => allRooms.Contains(br.RoomId) && br.BookingStatus != BookingRoomStatus.Cancelled)
            .Where(br => d < br.EndDate.Date && d >= br.StartDate.Date)
            .Select(br => br.RoomId)
            .Distinct()
            .ToListAsync();
        var availableRooms = totalRooms - overlappingRoomIds.Count;
        var availabilityPercent = (double)availableRooms / totalRooms * 100.0;
        string tier;
        double feePct;
        if (availabilityPercent <= 40.0) { tier = "0-40"; feePct = 0.5; }
        else if (availabilityPercent <= 80.0) { tier = "41-80"; feePct = 0.25; }
        else { tier = "81-100"; feePct = 0.1; }
        decimal feeAmount = 0;
        foreach (var rt in booking.BookingRoomTypes)
        {
            var nightly = rt.Price;
            foreach (var br in rt.BookingRooms)
            {
                if (br.ActualCheckOutAt.HasValue) continue;
                if (d >= br.EndDate.Date) continue;
                var remaining = (br.EndDate.Date - d).Days;
                if (remaining <= 0) continue;
                var baseAmount = nightly * remaining;
                feeAmount += baseAmount * (decimal)feePct;
            }
        }
        var resp = new EarlyCheckoutFeeResponseDto
        {
            AvailabilityPercent = Math.Round(availabilityPercent, 2),
            Tier = tier,
            FeePercentage = feePct * 100.0,
            FeeAmount = Math.Round(feeAmount, 2)
        };
        return ApiResponse<EarlyCheckoutFeeResponseDto>.Ok(resp);
    }

    public async Task<bool> AutoCancelBookingAsync(Guid hotelId)
    {
        try
        {
            var q = _bookingRepo.Query()
                .Include(x => x.PrimaryGuest)
                .Include(b => b.BookingRoomTypes)
                .ThenInclude(rt => rt.BookingRooms)
                .Where(x => x.HotelId == hotelId)
                .Where(x => x.Status != BookingStatus.Completed && x.Status != BookingStatus.Cancelled);

            var items = await q.ToListAsync();
            foreach (var item in items)
            {
                var roomTypes = await _bookingRoomTypeRepo.Query().Where(x => x.BookingId == item.Id).ToListAsync();
                foreach (var rt in roomTypes)
                {
                    var bookingRooms = await _bookingRoomRepo.Query()
                        .Where(x => x.BookingRoomTypeId == rt.BookingRoomTypeId)
                        .Select(x => new BookingRoomStatusDto(x.BookingStatus, rt.StartDate, x.ActualCheckInAt))
                        .ToListAsync();


                    if (item.StartDate?.Date.AddDays(1).AddTicks(-1) <= DateTime.Now && (bookingRooms.Count == 0 || bookingRooms.All(x => x.ActualCheckInAt == null)))
                    {
                        item.Status = BookingStatus.Missing;
                        await _bookingRepo.UpdateAsync(item);
                        await _bookingRepo.SaveChangesAsync();
                    }
                }

            }
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
