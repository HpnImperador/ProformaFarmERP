using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ProformaFarm.Application.Common;
using ProformaFarm.Application.Common.Exceptions;

namespace ProformaFarm.Middlewares;

public sealed class ExceptionMiddleware
{
    private const string CorrelationHeader = "X-Correlation-Id";

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Invoke(HttpContext context)
    {
        var correlationId = EnsureCorrelationId(context);

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["Path"] = context.Request.Path.Value,
            ["Method"] = context.Request.Method
        }))
        {
            try
            {
                await _next(context);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                // Client cancelou a requisição (timeout do gateway, navegador fechou, etc.)
                // Não é erro do servidor -> evita poluir log e evita 500 indevido.
                if (!context.Response.HasStarted)
                {
                    context.Response.Clear();
                    context.Response.StatusCode = 499; // de facto standard: Client Closed Request
                    context.Response.Headers[CorrelationHeader] = correlationId;
                }
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex, correlationId);
            }
        }
    }

    private static string EnsureCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationHeader, out var incoming) &&
            !string.IsNullOrWhiteSpace(incoming))
        {
            var cid = incoming.ToString();
            context.Response.Headers[CorrelationHeader] = cid;
            return cid;
        }

        var generated = Activity.Current?.Id ?? context.TraceIdentifier;
        context.Response.Headers[CorrelationHeader] = generated;
        return generated;
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex, string correlationId)
    {
        if (context.Response.HasStarted)
        {
            _logger.LogWarning(ex, "Response already started; cannot write error body.");
            return;
        }

        context.Response.Clear();
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers[CorrelationHeader] = correlationId;

        var mapped = MapException(ex);
        context.Response.StatusCode = mapped.StatusCode;

        if (mapped.StatusCode >= 500)
            _logger.LogError(ex, "Unhandled error. Status={Status} Code={Code}", mapped.StatusCode, mapped.Code);
        else
            _logger.LogInformation(ex, "Handled error. Status={Status} Code={Code}", mapped.StatusCode, mapped.Code);

        // Se houver data (ex.: erros de validação), devolver em ApiResponse<T>.Data
        if (mapped.Data is not null)
        {
            var payload = ApiResponse<object>.Fail(mapped.Message, mapped.Code, correlationId, mapped.Data);
            await context.Response.WriteAsJsonAsync(payload);
            return;
        }

        await context.Response.WriteAsJsonAsync(ApiResponse.Fail(mapped.Message, mapped.Code, correlationId));
    }

    private static ExceptionMapping MapException(Exception ex)
    {
        return ex switch
        {
            ValidationException vex => new ExceptionMapping(
                StatusCode: (int)HttpStatusCode.BadRequest,
                Code: vex.Code,
                Message: vex.Message,
                Data: vex.Errors
            ),

            NotFoundException nfx => new ExceptionMapping(
                StatusCode: (int)HttpStatusCode.NotFound,
                Code: nfx.Code,
                Message: nfx.Message,
                Data: null
            ),

            UnauthorizedAppException uex => new ExceptionMapping(
                StatusCode: (int)HttpStatusCode.Unauthorized,
                Code: uex.Code,
                Message: uex.Message,
                Data: null
            ),

            ForbiddenAppException fex => new ExceptionMapping(
                StatusCode: (int)HttpStatusCode.Forbidden,
                Code: fex.Code,
                Message: fex.Message,
                Data: null
            ),

            ConflictException cex => new ExceptionMapping(
                StatusCode: (int)HttpStatusCode.Conflict,
                Code: cex.Code,
                Message: cex.Message,
                Data: null
            ),

            // SQL Server: duplicate key (unique index / constraint)
            SqlException sqlEx when sqlEx.Number is 2627 or 2601 => new ExceptionMapping(
                StatusCode: (int)HttpStatusCode.Conflict,
                Code: "DUPLICATE_KEY",
                Message: "Violação de chave única.",
                Data: null
            ),

            TimeoutException => new ExceptionMapping(
                StatusCode: (int)HttpStatusCode.RequestTimeout,
                Code: "TIMEOUT",
                Message: "Tempo limite da requisição excedido.",
                Data: null
            ),

            UnauthorizedAccessException => new ExceptionMapping(
                StatusCode: (int)HttpStatusCode.Unauthorized,
                Code: "UNAUTHORIZED",
                Message: "Não autorizado.",
                Data: null
            ),

            _ => new ExceptionMapping(
                StatusCode: (int)HttpStatusCode.InternalServerError,
                Code: "INTERNAL_ERROR",
                Message: "Erro interno do servidor.",
                Data: null
            )
        };
    }

    private readonly record struct ExceptionMapping(int StatusCode, string Code, string Message, object? Data);
}
