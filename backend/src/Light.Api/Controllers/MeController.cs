using Light.Api.Common;
using Light.Api.Infrastructure.Auth;
using Light.Api.Models.Dto;
using Light.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Light.Api.Controllers;

/// <summary>当前用户信息（api-spec §4）。默认鉴权（FallbackPolicy），无需再标 [Authorize]。</summary>
[ApiController]
[Route("api/me")]
public sealed class MeController : ControllerBase
{
    private readonly UserRepository _users;

    public MeController(UserRepository users)
    {
        _users = users;
    }

    private long UserId => long.Parse(User.FindFirst("sub")?.Value
        ?? throw new InvalidOperationException("缺少用户身份"));

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var user = await _users.FindByIdAsync(UserId, cancellationToken)
            ?? throw new InvalidOperationException("用户不存在");
        var data = new UserBriefResponse(
            user.PublicId, user.Nickname,
            Mask(user.Phone), Mask(user.Email),
            user.PrivacyConsentAt);
        return Ok(data.ToOk(HttpContext));
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateMeRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Nickname) && request.Nickname.Length > 64)
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.InvalidArgument, "昵称过长", HttpContext.TraceIdentifier));
        }
        await _users.UpdateProfileAsync(UserId, request.Nickname?.Trim(), cancellationToken);
        return Ok(new { done = true }.ToOk(HttpContext));
    }

    [HttpPost("privacy-consent")]
    public async Task<IActionResult> PrivacyConsent(CancellationToken cancellationToken)
    {
        await _users.RecordPrivacyConsentAsync(UserId, cancellationToken);
        return Ok(new { done = true }.ToOk(HttpContext));
    }

    private static string? Mask(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        return value.Contains('@')
            ? $"{value[..2]}****{value[(value.IndexOf('@') - 1)..]}"
            : $"{value[..3]}****{value[^2..]}";
    }
}

public sealed record UpdateMeRequest(string? Nickname);
