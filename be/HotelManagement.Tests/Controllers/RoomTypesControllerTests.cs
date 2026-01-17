using HotelManagement.Api.Controllers;
using HotelManagement.Services.Admin.RoomTypes;
using HotelManagement.Services.Admin.RoomTypes.Dtos;
using HotelManagement.Services.Common;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace HotelManagement.Tests.Controllers;

public class RoomTypesControllerTests
{
    private static RoomTypesController CreateController(Mock<IRoomTypeService> mock)
    {
        return new RoomTypesController(mock.Object);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenModelInvalid()
    {
        var mock = new Mock<IRoomTypeService>();
        var controller = CreateController(mock);
        controller.ModelState.AddModelError("Name", "Required");
        var result = await controller.CreateRoomType(new CreateRoomTypeDto());
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Create_ReturnsCreatedOrBad(bool success)
    {
        var mock = new Mock<IRoomTypeService>();
        var resp = success ? ApiResponse<RoomTypeDto>.Ok(new RoomTypeDto { Id = Guid.NewGuid() }) : ApiResponse<RoomTypeDto>.Fail("fail");
        mock.Setup(s => s.CreateAsync(It.IsAny<CreateRoomTypeDto>())).ReturnsAsync(resp);
        var controller = CreateController(mock);
        var result = await controller.CreateRoomType(new CreateRoomTypeDto { HotelId = Guid.NewGuid(), Name = "Deluxe" });
        if (success) Assert.IsType<CreatedAtActionResult>(result); else Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenModelInvalid()
    {
        var mock = new Mock<IRoomTypeService>();
        var controller = CreateController(mock);
        controller.ModelState.AddModelError("Name", "Required");
        var result = await controller.UpdateRoomType(Guid.NewGuid(), new UpdateRoomTypeDto());
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Update_ReturnsOkOrBad(bool success)
    {
        var mock = new Mock<IRoomTypeService>();
        var resp = success ? ApiResponse<RoomTypeDto>.Ok(new RoomTypeDto()) : ApiResponse<RoomTypeDto>.Fail("fail");
        mock.Setup(s => s.UpdateAsync(It.IsAny<Guid>(), It.IsAny<UpdateRoomTypeDto>(), It.IsAny<Guid?>(), It.IsAny<string?>())).ReturnsAsync(resp);
        var controller = CreateController(mock);
        var result = await controller.UpdateRoomType(Guid.NewGuid(), new UpdateRoomTypeDto());
        if (success) Assert.IsType<OkObjectResult>(result); else Assert.IsType<BadRequestObjectResult>(result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Delete_ReturnsOkOrBad(bool success)
    {
        var mock = new Mock<IRoomTypeService>();
        var resp = success ? ApiResponse.Ok() : ApiResponse.Fail("fail");
        mock.Setup(s => s.DeleteAsync(It.IsAny<Guid>())).ReturnsAsync(resp);
        var controller = CreateController(mock);
        var result = await controller.DeleteRoomType(Guid.NewGuid());
        if (success) Assert.IsType<OkObjectResult>(result); else Assert.IsType<BadRequestObjectResult>(result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetById_ReturnsOkOrNotFound(bool success)
    {
        var mock = new Mock<IRoomTypeService>();
        var resp = success ? ApiResponse<RoomTypeDto>.Ok(new RoomTypeDto()) : ApiResponse<RoomTypeDto>.Fail("not found");
        mock.Setup(s => s.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(resp);
        var controller = CreateController(mock);
        var result = await controller.GetRoomTypeById(Guid.NewGuid());
        if (success) Assert.IsType<OkObjectResult>(result); else Assert.IsType<NotFoundObjectResult>(result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetDetails_ReturnsOkOrNotFound(bool success)
    {
        var mock = new Mock<IRoomTypeService>();
        var resp = success ? ApiResponse<RoomTypeDetailDto>.Ok(new RoomTypeDetailDto()) : ApiResponse<RoomTypeDetailDto>.Fail("not found");
        mock.Setup(s => s.GetDetailByIdAsync(It.IsAny<Guid>())).ReturnsAsync(resp);
        var controller = CreateController(mock);
        var result = await controller.GetRoomTypeDetails(Guid.NewGuid());
        if (success) Assert.IsType<OkObjectResult>(result); else Assert.IsType<NotFoundObjectResult>(result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetAll_ReturnsOkOrBad(bool success)
    {
        var mock = new Mock<IRoomTypeService>();
        var resp = success ? ApiResponse<List<RoomTypeDto>>.Ok(new List<RoomTypeDto>()) : ApiResponse<List<RoomTypeDto>>.Fail("fail");
        mock.Setup(s => s.GetAllAsync(It.IsAny<RoomTypeQueryDto>())).ReturnsAsync(resp);
        var controller = CreateController(mock);
        var result = await controller.GetRoomTypes(new RoomTypeQueryDto());
        if (success) Assert.IsType<OkObjectResult>(result); else Assert.IsType<BadRequestObjectResult>(result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetByHotel_ReturnsOkOrBad(bool success)
    {
        var mock = new Mock<IRoomTypeService>();
        var resp = success ? ApiResponse<List<RoomTypeDto>>.Ok(new List<RoomTypeDto>()) : ApiResponse<List<RoomTypeDto>>.Fail("fail");
        mock.Setup(s => s.GetByHotelIdAsync(It.IsAny<Guid>())).ReturnsAsync(resp);
        var controller = CreateController(mock);
        var result = await controller.GetRoomTypesByHotel(Guid.NewGuid());
        if (success) Assert.IsType<OkObjectResult>(result); else Assert.IsType<BadRequestObjectResult>(result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ValidateDelete_ReturnsOkOrBad(bool success)
    {
        var mock = new Mock<IRoomTypeService>();
        var resp = success ? ApiResponse.Ok() : ApiResponse.Fail("fail");
        mock.Setup(s => s.ValidateDeleteAsync(It.IsAny<Guid>())).ReturnsAsync(resp);
        var controller = CreateController(mock);
        var result = await controller.ValidateDelete(Guid.NewGuid());
        if (success) Assert.IsType<OkObjectResult>(result); else Assert.IsType<BadRequestObjectResult>(result);
    }
}
