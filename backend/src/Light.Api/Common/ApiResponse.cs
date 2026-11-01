using Microsoft.AspNetCore.Http;

namespace Light.Api.Common;

/// <summary>
/// 统一返回体：{ code, message, data, traceId }（CONVENTIONS.md 第 5 节）。
/// code = 0 表示成功；非 0 为业务错误码（见 ErrorCodes）。
/// </summary>
public sealed class ApiResponse<T>
{
    public int Code { get; init; }

    public string Message { get; init; } = "ok";

    public T? Data { get; init; }

    public string TraceId { get; init; } = string.Empty;

    public static ApiResponse<T> Ok(T? data, string traceId) =>
        new() { Code = 0, Message = "ok", Data = data, TraceId = traceId };

    public static ApiResponse<T> Fail(int code, string message, string traceId) =>
        new() { Code = code, Message = message, Data = default, TraceId = traceId };
}

/// <summary>控制器侧便捷构造：从 HttpContext 取 TraceIdentifier。</summary>
public static class ApiResponseExtensions
{
    public static ApiResponse<T> ToOk<T>(this T? data, HttpContext context) =>
        ApiResponse<T>.Ok(data, context.TraceIdentifier);
}
