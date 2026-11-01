using FluentValidation;
using Light.Api.Common;
using Light.Api.Models.Dto;
using Light.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Light.Api.Controllers;

/// <summary>认证接口（api-spec §4）：除登出外全部匿名。</summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AuthService _auth;

    public AuthController(AuthService auth)
    {
        _auth = auth;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        [FromServices] IValidator<RegisterRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.InvalidArgument, validation.Errors[0].ErrorMessage, HttpContext.TraceIdentifier));
        }
        return Ok((await _auth.RegisterAsync(request, cancellationToken)).ToOk(HttpContext));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        [FromServices] IValidator<LoginRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.InvalidArgument, validation.Errors[0].ErrorMessage, HttpContext.TraceIdentifier));
        }
        return Ok((await _auth.LoginAsync(request, cancellationToken)).ToOk(HttpContext));
    }

    [HttpPost("sms-code")]
    [AllowAnonymous]
    public async Task<IActionResult> SmsCode(
        [FromBody] SmsCodeRequest request,
        [FromServices] IValidator<SmsCodeRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.InvalidArgument, validation.Errors[0].ErrorMessage, HttpContext.TraceIdentifier));
        }
        await _auth.SendSmsCodeAsync(request.Phone, cancellationToken);
        var data = new { sent = true, mockCode = _auth.MockLastCode };
        return Ok(data.ToOk(HttpContext));
    }

    [HttpPost("login/sms")]
    [AllowAnonymous]
    public async Task<IActionResult> SmsLogin(
        [FromBody] SmsLoginRequest request,
        [FromServices] IValidator<SmsLoginRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.InvalidArgument, validation.Errors[0].ErrorMessage, HttpContext.TraceIdentifier));
        }
        return Ok((await _auth.SmsLoginAsync(request, cancellationToken)).ToOk(HttpContext));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshRequest request,
        [FromServices] IValidator<RefreshRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.InvalidArgument, validation.Errors[0].ErrorMessage, HttpContext.TraceIdentifier));
        }
        return Ok((await _auth.RefreshAsync(request.RefreshToken, cancellationToken)).ToOk(HttpContext));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken cancellationToken)
    {
        await _auth.LogoutAsync(request.RefreshToken, cancellationToken);
        return Ok(new { done = true }.ToOk(HttpContext));
    }
}
