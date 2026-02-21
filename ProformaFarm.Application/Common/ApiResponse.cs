using System;
using System.Collections.Generic;
using System.Text;

namespace ProformaFarm.Application.Common;

public sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    public string Code { get; init; } = "OK";
    public string Message { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public T? Data { get; init; }

    public static ApiResponse<T> Ok(T data, string message = "OK", string? correlationId = null, string code = "OK")
        => new()
        {
            Success = true,
            Code = code,
            Message = message,
            Data = data,
            CorrelationId = correlationId ?? string.Empty
        };

    public static ApiResponse<T> Fail(
    string message,
    string code = "ERROR",
    string? correlationId = null,
    T? data = default
) => new()
{
    Success = false,
    Code = code,
    Message = message,
    Data = data,
    CorrelationId = correlationId ?? string.Empty
};

}

public static class ApiResponse
{
    public static ApiResponse<object> Ok(string message = "OK", string? correlationId = null, string code = "OK")
        => ApiResponse<object>.Ok(new { }, message, correlationId, code);

    public static ApiResponse<object> Fail(string message, string code = "ERROR", string? correlationId = null)
        => ApiResponse<object>.Fail(message, code, correlationId);
}
