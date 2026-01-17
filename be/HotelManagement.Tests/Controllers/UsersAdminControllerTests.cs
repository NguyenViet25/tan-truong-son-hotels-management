using HotelManagement.Api.Controllers;
using HotelManagement.Services.Admin.Users;
using HotelManagement.Services.Admin.Users.Dtos;
using HotelManagement.Services.Common;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace HotelManagement.Tests.Controllers;

public class UsersAdminControllerTests
{
    private static UsersAdminController CreateController(Mock<IUsersAdminService> mock)
    {
        return new UsersAdminController(mock.Object);
    }

    [Fact]
    public async Task List_ReturnsOk()
    {
        var mock = new Mock<IUsersAdminService>();
        mock.Setup(s => s.ListAsync(It.IsAny<UsersQueryDto>()))
            .ReturnsAsync((new List<UserSummaryDto> { new UserSummaryDto(Guid.NewGuid(), "user","email","0123","Full Name", true, null, Enumerable.Empty<string>(), Enumerable.Empty<UserPropertyRoleDto>()) }, 1));
        var controller = CreateController(mock);
        var result = await controller.List(new UsersQueryDto());
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task ListHouseKeepers_ReturnsOk()
    {
        var mock = new Mock<IUsersAdminService>();
        mock.Setup(s => s.ListByRoleAsync(It.IsAny<UserByRoleQuery>(), It.IsAny<Guid>()))
            .ReturnsAsync(new List<UserSummaryDto> { new UserSummaryDto(Guid.NewGuid(), "user","email","0123","Full Name", true, null, Enumerable.Empty<string>(), Enumerable.Empty<UserPropertyRoleDto>()) });
        var controller = CreateController(mock);
        var result = await controller.ListHouseKeppers(new UserByRoleQuery(Guid.NewGuid(), "Housekeeper"));
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000100", 1, 10, null, null)]
    [InlineData("00000000-0000-0000-0000-000000000101", 2, 20, "manager", "alex")]
    [InlineData("00000000-0000-0000-0000-000000000102", 3, 30, "housekeeper", "mai")]
    [InlineData("00000000-0000-0000-0000-000000000103", 4, 40, "receptionist", null)]
    [InlineData("00000000-0000-0000-0000-000000000104", 5, 50, null, "bob")]
    [InlineData("00000000-0000-0000-0000-000000000105", 6, 15, "waiter", "linh")]
    [InlineData("00000000-0000-0000-0000-000000000106", 7, 25, "chef", "an")]
    [InlineData("00000000-0000-0000-0000-000000000107", 8, 35, "security", "tam")]
    [InlineData("00000000-0000-0000-0000-000000000108", 9, 45, "accountant", "nga")]
    [InlineData("00000000-0000-0000-0000-000000000109", 10, 55, null, null)]
    public async Task ListByHotels_ReturnsOk(string hotelId, int page, int pageSize, string? role, string? search)
    {
        var mock = new Mock<IUsersAdminService>();
        mock.Setup(s => s.ListByHotelAsync(It.IsAny<UsersQueryDto>(), It.IsAny<Guid>()))
            .ReturnsAsync((new List<UserSummaryDto> { new UserSummaryDto(Guid.NewGuid(), "user","email","0123","Full Name", true, null, Enumerable.Empty<string>(), Enumerable.Empty<UserPropertyRoleDto>()) }, 1));
        var controller = CreateController(mock);
        var query = new UsersQueryDto(page, pageSize, search, role, null, null);
        var result = await controller.ListByHotels(Guid.Parse(hotelId), query);
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ListWaiter_ReturnsOkOrBad(bool hasClaim)
    {
        var mock = new Mock<IUsersAdminService>();
        mock.Setup(s => s.ListByHotelAsync(It.IsAny<UsersQueryDto>(), It.IsAny<Guid>()))
            .ReturnsAsync((new List<UserSummaryDto> { new UserSummaryDto(Guid.NewGuid(), "user","email","0123","Full Name", true, null, Enumerable.Empty<string>(), Enumerable.Empty<UserPropertyRoleDto>()) }, 1));
        var controller = CreateController(mock);
        var context = new DefaultHttpContext();
        if (hasClaim)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("hotelId", Guid.NewGuid().ToString()) }));
        }
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        var result = await controller.ListWaiter();
        if (hasClaim) Assert.IsType<OkObjectResult>(result.Result); else Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Get_ReturnsOkOrNotFound(bool found)
    {
        var mock = new Mock<IUsersAdminService>();
        var dto = found ? new UserDetailsDto(Guid.NewGuid(), "user", "email", "0123", true, null, Enumerable.Empty<string>(), Enumerable.Empty<UserPropertyRoleDto>()) : null;
        mock.Setup(s => s.GetAsync(It.IsAny<Guid>())).ReturnsAsync(dto);
        var controller = CreateController(mock);
        var result = await controller.Get(Guid.NewGuid());
        if (found) Assert.IsType<OkObjectResult>(result.Result); else Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Create_ReturnsCreated()
    {
        var mock = new Mock<IUsersAdminService>();
        mock.Setup(s => s.CreateAsync(It.IsAny<CreateUserDto>())).ReturnsAsync(new UserDetailsDto(Guid.NewGuid(), "user", "email", "0123", true, null, Enumerable.Empty<string>(), Enumerable.Empty<UserPropertyRoleDto>()));
        var controller = CreateController(mock);
        var result = await controller.Create(new CreateUserDto("user","email","Full Name", "0123", null, null));
        Assert.IsType<CreatedAtActionResult>(result.Result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Update_ReturnsOkOrNotFound(bool found)
    {
        var mock = new Mock<IUsersAdminService>();
        var dto = found ? new UserDetailsDto(Guid.NewGuid(), "user", "email", "0123", true, null, Enumerable.Empty<string>(), Enumerable.Empty<UserPropertyRoleDto>()) : null;
        mock.Setup(s => s.UpdateAsync(It.IsAny<Guid>(), It.IsAny<UpdateUserDto>())).ReturnsAsync(dto);
        var controller = CreateController(mock);
        var result = await controller.Update(Guid.NewGuid(), new UpdateUserDto("Full Name","email","0123", null, null));
        if (found) Assert.IsType<OkObjectResult>(result.Result); else Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Lock_ReturnsOkOrNotFound(bool ok)
    {
        var mock = new Mock<IUsersAdminService>();
        mock.Setup(s => s.LockAsync(It.IsAny<Guid>(), It.IsAny<LockUserDto>())).ReturnsAsync(ok);
        var controller = CreateController(mock);
        var result = await controller.Lock(Guid.NewGuid(), new LockUserDto(DateTimeOffset.UtcNow));
        if (ok) Assert.IsType<OkObjectResult>(result.Result); else Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Unlock_ReturnsOkOrNotFound(bool ok)
    {
        var mock = new Mock<IUsersAdminService>();
        mock.Setup(s => s.UnLockAsync(It.IsAny<Guid>(), It.IsAny<LockUserDto>())).ReturnsAsync(ok);
        var controller = CreateController(mock);
        var result = await controller.UnLock(Guid.NewGuid(), new LockUserDto(DateTimeOffset.UtcNow));
        if (ok) Assert.IsType<OkObjectResult>(result.Result); else Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ResetPassword_ReturnsOkOrNotFound(bool ok)
    {
        var mock = new Mock<IUsersAdminService>();
        mock.Setup(s => s.ResetPasswordAsync(It.IsAny<Guid>(), It.IsAny<ResetPasswordAdminDto>())).ReturnsAsync(ok);
        var controller = CreateController(mock);
        var result = await controller.ResetPassword(Guid.NewGuid(), new ResetPasswordAdminDto("new"));
        if (ok) Assert.IsType<OkObjectResult>(result.Result); else Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AssignPropertyRole_ReturnsOkOrBad(bool ok)
    {
        var mock = new Mock<IUsersAdminService>();
        var dto = ok ? new UserPropertyRoleDto(Guid.NewGuid(), Guid.NewGuid(), HotelManagement.Domain.UserRole.Manager, "Name") : null;
        mock.Setup(s => s.AssignPropertyRoleAsync(It.IsAny<Guid>(), It.IsAny<AssignPropertyRoleDto>())).ReturnsAsync(dto);
        var controller = CreateController(mock);
        var result = await controller.AssignPropertyRole(Guid.NewGuid(), new AssignPropertyRoleDto(Guid.NewGuid(), HotelManagement.Domain.UserRole.Manager));
        if (ok) Assert.IsType<OkObjectResult>(result.Result); else Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RemovePropertyRole_ReturnsOkOrNotFound(bool ok)
    {
        var mock = new Mock<IUsersAdminService>();
        mock.Setup(s => s.RemovePropertyRoleAsync(It.IsAny<Guid>(), It.IsAny<Guid>())).ReturnsAsync(ok);
        var controller = CreateController(mock);
        var result = await controller.RemovePropertyRole(Guid.NewGuid(), Guid.NewGuid());
        if (ok) Assert.IsType<OkObjectResult>(result.Result); else Assert.IsType<NotFoundObjectResult>(result.Result);
    }
}
