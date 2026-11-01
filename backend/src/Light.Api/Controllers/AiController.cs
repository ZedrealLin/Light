using FluentValidation;
using Light.Api.Common;
using Light.Api.Models.Dto;
using Light.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Light.Api.Controllers;

/// <summary>AI 陪伴（api-spec §8）：默认鉴权；危机命中返回 7101（不调用模型）。</summary>
[ApiController]
[Route("api/ai")]
public sealed class AiController : ControllerBase
{
    private readonly AiGatewayService _gateway;

    public AiController(AiGatewayService gateway)
    {
        _gateway = gateway;
    }

    private long UserId => long.Parse(User.FindFirst("sub")?.Value
        ?? throw new InvalidOperationException("缺少用户身份"));

    [HttpPost("sessions")]
    public async Task<IActionResult> CreateSession(CancellationToken cancellationToken)
    {
        var created = await _gateway.CreateSessionAsync(UserId, cancellationToken);
        return Ok(created.ToOk(HttpContext));
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> Sessions(CancellationToken cancellationToken)
    {
        var sessions = await _gateway.ListSessionsAsync(UserId, cancellationToken);
        return Ok(sessions.ToOk(HttpContext));
    }

    [HttpPost("sessions/{sessionId:long}/messages")]
    [Consumes("application/json")]
    public async Task<IActionResult> Send(
        long sessionId,
        [FromBody] AiSendMessageRequest request,
        [FromServices] IValidator<AiSendMessageRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail(ErrorCodes.InvalidArgument, validation.Errors[0].ErrorMessage, HttpContext.TraceIdentifier));
        }
        try
        {
            var reply = await _gateway.SendAsync(UserId, sessionId, request.Content, cancellationToken);
            return Ok(reply.ToOk(HttpContext));
        }
        catch (AiCrisisException ex)
        {
            // 硬性规则 2：命中危机 → code=7101 + 固定安全话术 + 求助入口（不调用模型）
            return Ok(new ApiResponse<object>
            {
                Code = ErrorCodes.AiCrisisRefusal,
                Message = "我们注意到你可能正处于困难时刻",
                Data = ex.Response,
                TraceId = HttpContext.TraceIdentifier,
            });
        }
        catch (BizException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Code, ex.Message, HttpContext.TraceIdentifier));
        }
    }

    [HttpGet("sessions/{sessionId:long}/messages")]
    public async Task<IActionResult> Messages(long sessionId, CancellationToken cancellationToken)
    {
        var messages = await _gateway.GetMessagesAsync(UserId, sessionId, cancellationToken);
        return messages is null
            ? BadRequest(ApiResponse<object>.Fail(ErrorCodes.NotFound, "会话不存在", HttpContext.TraceIdentifier))
            : Ok(messages.ToOk(HttpContext));
    }
}
