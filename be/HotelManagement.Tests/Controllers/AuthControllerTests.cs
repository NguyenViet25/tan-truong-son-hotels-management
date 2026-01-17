using HotelManagement.Api.Controllers;
using HotelManagement.Services.Admin.Bookings;
using HotelManagement.Services.Auth;
using HotelManagement.Services.Auth.Dtos;
using HotelManagement.Services.Common;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace HotelManagement.Tests.Controllers;

public class AuthControllerTests
{
    private static AuthController CreateController(Mock<IAuthService> mock)
    {
        var bs = new Mock<IBookingsService>();
        return new AuthController(mock.Object, bs.Object);
    }

    [Fact]
    public async Task Login_ReturnsTwoFactor_WhenRequired()
    {
        var mock = new Mock<IAuthService>();
        var bs = new Mock<IBookingsService>();
        mock.Setup(a => a.LoginAsync(It.IsAny<LoginRequestDto>()))
            .ReturnsAsync(new LoginResponseDto(true, null, null, null));
        var controller = CreateController(mock);
        var result = await controller.Login(new LoginRequestDto("u","p"));
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ApiResponse<LoginResponseDto>>(ok.Value);
        Assert.True(payload.IsSuccess);
        Assert.True(payload.Data!.RequiresTwoFactor);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenLocked()
    {
        var mock = new Mock<IAuthService>();
        mock.Setup(a => a.LoginAsync(It.IsAny<LoginRequestDto>()))
            .ReturnsAsync(new LoginResponseDto(false, null, DateTimeOffset.UtcNow.AddMinutes(5), null));
        var controller = CreateController(mock);
        var result = await controller.Login(new LoginRequestDto("u","p"));
        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task Login_ReturnsOk_WhenTokenPresent()
    {
        var mock = new Mock<IAuthService>();
        mock.Setup(a => a.LoginAsync(It.IsAny<LoginRequestDto>()))
            .ReturnsAsync(new LoginResponseDto(false, "token", null, null));
        var controller = CreateController(mock);
        var result = await controller.Login(new LoginRequestDto("u","p"));
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task Logout_ReturnsOk()
    {
        var mock = new Mock<IAuthService>();
        var controller = CreateController(mock);
        var ctx = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        ctx.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity());
        controller.ControllerContext = new ControllerContext { HttpContext = ctx };
        var res = await controller.Logout();
        Assert.IsType<OkObjectResult>(res.Result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ForgotPassword_ReturnsOkOrNotFound(bool ok)
    {
        var mock = new Mock<IAuthService>();
        mock.Setup(a => a.SendForgotPasswordOtpAsync(It.IsAny<ForgotPasswordRequestDto>())).ReturnsAsync(ok);
        var controller = CreateController(mock);
        var result = await controller.ForgotPassword(new ForgotPasswordRequestDto("user"));
        if (ok) Assert.IsType<OkObjectResult>(result.Result); else Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ResetPassword_ReturnsOkOrBad(bool ok)
    {
        var mock = new Mock<IAuthService>();
        mock.Setup(a => a.ResetPasswordAsync(It.IsAny<ResetPasswordRequestDto>())).ReturnsAsync(ok);
        var controller = CreateController(mock);
        var result = await controller.ResetPassword(new ResetPasswordRequestDto("u","code","new"));
        if (ok) Assert.IsType<OkObjectResult>(result.Result); else Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}
