using HotelManagement.Api.Controllers;
using HotelManagement.Services.Admin.Rooms;
using HotelManagement.Services.Admin.Rooms.Dtos;
using HotelManagement.Services.Common;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace HotelManagement.Tests.Controllers;

public class RoomsControllerTests
{
    private static RoomsController CreateController(Mock<IRoomsService> mock)
    {
        return new RoomsController(mock.Object);
    }

    [Theory]
    [InlineData(null, null, null, null, null)]
    [InlineData("Available", null, null, null, "101")]
    [InlineData("Clean", null, null, 2, null)]
    [InlineData("Dirty", null, null, 5, "A")]
    [InlineData("OutOfService", null, null, null, null)]
    [InlineData("Available", "00000000-0000-0000-0000-000000000010", null, 1, "10")]
    [InlineData("Available", null, "00000000-0000-0000-0000-000000000011", 2, "11")]
    [InlineData("Clean", "00000000-0000-0000-0000-000000000012", "00000000-0000-0000-0000-000000000013", 3, "12")]
    [InlineData("Dirty", "00000000-0000-0000-0000-000000000014", null, 4, "14")]
    [InlineData("OutOfService", null, "00000000-0000-0000-0000-000000000015", 5, "15")]
    [InlineData(null, "00000000-0000-0000-0000-000000000016", null, null, "B")]
    [InlineData(null, null, "00000000-0000-0000-0000-000000000017", null, "C")]
    [InlineData("Available", null, null, 6, "201")]
    [InlineData("Clean", null, null, 7, "202")]
    [InlineData("Dirty", null, null, 8, "203")]
    [InlineData("OutOfService", null, null, 9, "204")]
    [InlineData("Available", null, null, 10, null)]
    [InlineData("Clean", null, null, 11, null)]
    [InlineData("Dirty", null, null, 12, null)]
    [InlineData("OutOfService", null, null, 13, null)]
    [InlineData(null, null, null, 14, "X")]
    [InlineData(null, null, null, 15, "Y")]
    [InlineData(null, null, null, 16, "Z")]
    [InlineData("Available", "00000000-0000-0000-0000-000000000018", "00000000-0000-0000-0000-000000000019", null, "A")]
    [InlineData("Clean", "00000000-0000-0000-0000-000000000020", "00000000-0000-0000-0000-000000000021", null, "B")]
    [InlineData("Dirty", "00000000-0000-0000-0000-000000000022", "00000000-0000-0000-0000-000000000023", null, "C")]
    [InlineData("OutOfService", "00000000-0000-0000-0000-000000000024", "00000000-0000-0000-0000-000000000025", null, "D")]
    [InlineData("Available", null, null, null, "alpha")]
    [InlineData("Clean", null, null, null, "beta")]
    [InlineData("Dirty", null, null, null, "gamma")]
    [InlineData("OutOfService", null, null, null, "delta")]
    [InlineData(null, "00000000-0000-0000-0000-000000000026", "00000000-0000-0000-0000-000000000027", 17, "301")]
    [InlineData(null, "00000000-0000-0000-0000-000000000028", "00000000-0000-0000-0000-000000000029", 18, "302")]
    [InlineData(null, "00000000-0000-0000-0000-000000000030", "00000000-0000-0000-0000-000000000031", 19, "303")]
    public async Task List_ReturnsOk(object? status, string? hotelId, string? roomTypeId, int? floor, string? search)
    {
        var mock = new Mock<IRoomsService>();
        mock.Setup(s => s.ListAsync(It.IsAny<RoomsQueryDto>()))
            .ReturnsAsync(ApiResponse<List<RoomSummaryDto>>.Ok(new List<RoomSummaryDto>{new RoomSummaryDto()}));
        var controller = CreateController(mock);

        var query = new RoomsQueryDto
        {
            HotelId = string.IsNullOrEmpty(hotelId) ? null : Guid.Parse(hotelId),
            RoomTypeId = string.IsNullOrEmpty(roomTypeId) ? null : Guid.Parse(roomTypeId),
            Floor = floor,
            Search = search,
            Status = status is null ? null : Enum.Parse<HotelManagement.Domain.RoomStatus>(status.ToString()!)
        };

        var result = await controller.List(query);
        Assert.IsType<OkObjectResult>(result.Result);
        var ok = (OkObjectResult)result.Result!;
        var payload = Assert.IsType<ApiResponse<List<RoomSummaryDto>>>(ok.Value);
        Assert.True(payload.IsSuccess);
        mock.Verify(s => s.ListAsync(It.IsAny<RoomsQueryDto>()), Times.Once);
    }

    [Theory]
    [InlineData("Available", null, null, 20, "401")]
    [InlineData("Clean", null, null, 21, "402")]
    [InlineData("Dirty", null, null, 22, "403")]
    [InlineData("OutOfService", null, null, 23, "404")]
    [InlineData(null, null, null, 24, null)]
    [InlineData(null, "00000000-0000-0000-0000-000000000032", null, 25, "A1")]
    [InlineData(null, null, "00000000-0000-0000-0000-000000000033", 26, "B2")]
    [InlineData("Available", "00000000-0000-0000-0000-000000000034", null, null, null)]
    [InlineData("Clean", null, "00000000-0000-0000-0000-000000000035", null, null)]
    [InlineData("Dirty", "00000000-0000-0000-0000-000000000036", "00000000-0000-0000-0000-000000000037", null, null)]
    public async Task List_ReturnsOk_Expanded(object? status, string? hotelId, string? roomTypeId, int? floor, string? search)
    {
        var mock = new Mock<IRoomsService>();
        mock.Setup(s => s.ListAsync(It.IsAny<RoomsQueryDto>()))
            .ReturnsAsync(ApiResponse<List<RoomSummaryDto>>.Ok(new List<RoomSummaryDto>{new RoomSummaryDto()}));
        var controller = CreateController(mock);
        var query = new RoomsQueryDto
        {
            HotelId = string.IsNullOrEmpty(hotelId) ? null : Guid.Parse(hotelId),
            RoomTypeId = string.IsNullOrEmpty(roomTypeId) ? null : Guid.Parse(roomTypeId),
            Floor = floor,
            Search = search,
            Status = status is null ? null : Enum.Parse<HotelManagement.Domain.RoomStatus>(status.ToString()!)
        };
        var result = await controller.List(query);
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    [InlineData("00000000-0000-0000-0000-000000000002")]
    [InlineData("00000000-0000-0000-0000-000000000003")]
    [InlineData("00000000-0000-0000-0000-000000000004")]
    [InlineData("00000000-0000-0000-0000-000000000005")]
    public async Task ListByType_ReturnsOk(string id)
    {
        var mock = new Mock<IRoomsService>();
        mock.Setup(s => s.ListByTypeAsync(It.IsAny<Guid>()))
            .ReturnsAsync(ApiResponse<List<RoomSummaryDto>>.Ok(new List<RoomSummaryDto> { new RoomSummaryDto() }));
        var controller = CreateController(mock);
        var result = await controller.ListRoomByType(Guid.Parse(id));
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task Get_ReturnsNotFound_WhenFail()
    {
        var mock = new Mock<IRoomsService>();
        mock.Setup(s => s.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(ApiResponse<RoomSummaryDto>.Fail("not found"));
        var controller = CreateController(mock);
        var result = await controller.Get(Guid.NewGuid());
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Get_ReturnsOk_WhenSuccess()
    {
        var mock = new Mock<IRoomsService>();
        mock.Setup(s => s.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(ApiResponse<RoomSummaryDto>.Ok(new RoomSummaryDto()));
        var controller = CreateController(mock);
        var result = await controller.Get(Guid.NewGuid());
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Create_ReturnsOkOrBad(bool success)
    {
        var mock = new Mock<IRoomsService>();
        var response = success ? ApiResponse<RoomSummaryDto>.Ok(new RoomSummaryDto()) : ApiResponse<RoomSummaryDto>.Fail("fail");
        mock.Setup(s => s.CreateAsync(It.IsAny<CreateRoomDto>())).ReturnsAsync(response);
        var controller = CreateController(mock);
        var dto = new CreateRoomDto { HotelId = Guid.NewGuid(), RoomTypeId = Guid.NewGuid(), Number = "101", Floor = 1 };
        var result = await controller.Create(dto);
        if (success) Assert.IsType<OkObjectResult>(result.Result); else Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Update_ReturnsOkOrBad(bool success)
    {
        var mock = new Mock<IRoomsService>();
        var response = success ? ApiResponse<RoomSummaryDto>.Ok(new RoomSummaryDto()) : ApiResponse<RoomSummaryDto>.Fail("fail");
        mock.Setup(s => s.UpdateAsync(It.IsAny<Guid>(), It.IsAny<UpdateRoomDto>())).ReturnsAsync(response);
        var controller = CreateController(mock);
        var dto = new UpdateRoomDto { Number = "102" };
        var result = await controller.Update(Guid.NewGuid(), dto);
        if (success) Assert.IsType<OkObjectResult>(result.Result); else Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Delete_ReturnsOkOrBad(bool success)
    {
        var mock = new Mock<IRoomsService>();
        var response = success ? ApiResponse.Ok() : ApiResponse.Fail("fail");
        mock.Setup(s => s.DeleteAsync(It.IsAny<Guid>())).ReturnsAsync(response);
        var controller = CreateController(mock);
        var result = await controller.Delete(Guid.NewGuid());
        if (success) Assert.IsType<OkObjectResult>(result.Result); else Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SetOutOfService_ReturnsOkOrBad(bool success)
    {
        var mock = new Mock<IRoomsService>();
        var response = success ? ApiResponse<RoomSummaryDto>.Ok(new RoomSummaryDto()) : ApiResponse<RoomSummaryDto>.Fail("fail");
        mock.Setup(s => s.SetOutOfServiceAsync(It.IsAny<Guid>(), It.IsAny<SetOutOfServiceDto>())).ReturnsAsync(response);
        var controller = CreateController(mock);
        var result = await controller.SetOutOfService(Guid.NewGuid(), new SetOutOfServiceDto { Reason = "Maintenance" });
        if (success) Assert.IsType<OkObjectResult>(result.Result); else Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}
