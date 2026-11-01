using Microsoft.AspNetCore.Http;

namespace Light.Api.Common;

/// <summary>
/// 全局异常中间件：任何未处理异常都返回统一返回体，
/// 对外只给"服务暂不可用"（code=1003），堆栈与内部细节只进 Serilog 日志，绝不外泄。
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
