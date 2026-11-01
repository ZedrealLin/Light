using Light.Api.Services;
using Microsoft.AspNetCore.Http;

namespace Light.Api.Common;

/// <summary>
/// 全局异常中间件：
/// - BizException（业务校验失败）→ 按其业务码返回 400 统一返回体；
/// - 其他未处理异常 → 1003"服务暂不可用"，堆栈只进 Serilog，绝不外泄。
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (BizException ex)
        {
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(
                ApiResponse<object>.Fail(ex.Code, ex.Message, context.TraceIdentifier));
        }
        catch (Exception ex)
        {
            var traceId = context.TraceIdentifier;
            _logger.LogError(
                ex,
                "未处理异常 traceId={TraceId} method={Method} path={Path}",
                traceId,
                context.Request.Method,
                context.Request.Path);

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            var payload = ApiResponse<object>.Fail(ErrorCodes.InternalError, "服务暂不可用", traceId);
            await context.Response.WriteAsJsonAsync(payload);
        }
    }
}
