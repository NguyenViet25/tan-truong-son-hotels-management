using HotelManagement.Services.Admin.Rooms;
using HotelManagement.Services.Admin.Rooms.Dtos;
using HotelManagement.Services.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelManagement.Api.Controllers;

[ApiController]
[Route("api/rooms")]
//[Authorize]

public class RoomsController : ControllerBase
{
    private readonly IRoomsService _roomsService;

    public RoomsController(IRoomsService roomsService)
    {
        _roomsService = roomsService;
    }

    // UC-23: List rooms by status, floor, type
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<RoomSummaryDto>>>> List([FromQuery] RoomsQueryDto query)
    {
        var result = await _roomsService.ListAsync(query);
        return Ok(result);
    }

    [HttpGet("by-type/{id}")]
    public async Task<ActionResult<ApiResponse<List<RoomSummaryDto>>>> ListRoomByType([FromRoute] Guid id)
    {
        var result = await _roomsService.ListByTypeAsync(id);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<RoomSummaryDto>>> Get(Guid id)
    {
        var result = await _roomsService.GetByIdAsync(id);
        if (result.IsSuccess) return Ok(result);
        return NotFound(result);
    }

    // UC-24: Create room with assigned type, floor, number
    [HttpPost]
    public async Task<ActionResult<ApiResponse<RoomSummaryDto>>> Create([FromBody] CreateRoomDto dto)
    {
        var result = await _roomsService.CreateAsync(dto);
        if (result.IsSuccess) return Ok(result);
        return BadRequest(result);
    }

    // UC-25: Edit room information and operational status
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<RoomSummaryDto>>> Update(Guid id, [FromBody] UpdateRoomDto dto)
    {
        var result = await _roomsService.UpdateAsync(id, dto);
        if (result.IsSuccess) return Ok(result);
        return BadRequest(result);
    }

    // UC-26: Delete room without booking history
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id)
    {
        var result = await _roomsService.DeleteAsync(id);
        if (result.IsSuccess) return Ok(result);
        return BadRequest(result);
    }

    // UC-27: Mark room temporarily out of service
    [HttpPost("{id}/out-of-service")]
    public async Task<ActionResult<ApiResponse<RoomSummaryDto>>> SetOutOfService(Guid id, [FromBody] SetOutOfServiceDto dto)
    {
        var result = await _roomsService.SetOutOfServiceAsync(id, dto);
        if (result.IsSuccess) return Ok(result);
        return BadRequest(result);
    }

}
