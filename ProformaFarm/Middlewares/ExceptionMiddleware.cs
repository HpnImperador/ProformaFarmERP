using System.Diagnostics;
using System.Net;
using Microsoft.Data.SqlClient;
using ProformaFarm.Application.Common.Exceptions;
using ProformaFarm.Application.Common.Responses;

namespace ProformaFarm.Middlewares;

public sealed class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        var correlationId = EnsureCorrelationId(context);

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleException(context, ex, correlationId);
        }
    }

    private static string EnsureCorrelationId(HttpContext context)
    {
        // Prefer header vindo de gateway/cliente; senão use TraceIdentifier.
        const string header = "X-Correlation-Id";

        if (context.Request.Headers.TryGetValue(header, out var incoming) && !string.IsNullOrWhiteSpace(incoming))
        {
            context.Response.Headers[header] = incoming.ToString();
            return incoming.ToString();
        }

        var generated = Activity.Current?.Id ?? context.TraceIdentifier;
        context.Response.Headers[header] = generated;
        return generated;
    }

    private async Task HandleException(HttpContext context, Exception ex, string correlationId)
    {
        if (context.Response.HasStarted)
        {
            _logger.LogWarning(ex,
                "Response already started. CorrelationId={CorrelationId}",
                correlationId);

            return;
        }

        context.Response.ContentType = "application/json; charset=utf-8";

        var (statusCode, code, message, extra) = MapException(ex);

        if (statusCode >= 500)
            _logger.LogError(ex,
                "Unhandled error. Status={Status} Code={Code} CorrelationId={CorrelationId}",
                statusCode, code, correlationId);
        else
            _logger.LogInformation(ex,
                "Handled error. Status={Status} Code={Code} CorrelationId={CorrelationId}",
                statusCode, code, correlationId);

        context.Response.StatusCode = statusCode;

        await context.Response.WriteAsJsonAsync(
            ApiResponse.Fail(message, code, correlationId));
    }


    private static (int statusCode, string code, string message, object? extra) MapException(Exception ex)
    {
        return ex switch
        {
            ValidationException vex => ((int)HttpStatusCode.BadRequest, vex.Code, vex.Message, vex.Errors),
            NotFoundException nfx => ((int)HttpStatusCode.NotFound, nfx.Code, nfx.Message, null),
            UnauthorizedAppException uex => ((int)HttpStatusCode.Unauthorized, uex.Code, uex.Message, null),
            ForbiddenAppException fex => ((int)HttpStatusCode.Forbidden, fex.Code, fex.Message, null),
            ConflictException cex => ((int)HttpStatusCode.Conflict, cex.Code, cex.Message, null),

            // SQL Server: erros comuns que você vai querer tratar
            SqlException sqlEx when sqlEx.Number is 2627 or 2601
                => ((int)HttpStatusCode.Conflict, "DUPLICATE_KEY", "Duplicate key violation", null),

            // Se for algo que você considera “infra”
            TimeoutException
                => ((int)HttpStatusCode.RequestTimeout, "TIMEOUT", "Request timed out", null),

            _ => ((int)HttpStatusCode.InternalServerError, "INTERNAL_ERROR", "Internal server error", null)
        };
    }
}
