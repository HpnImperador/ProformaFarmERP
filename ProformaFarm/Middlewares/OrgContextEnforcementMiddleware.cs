using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using ProformaFarm.Application.Common;
using ProformaFarm.Application.Interfaces.Context;

namespace ProformaFarm.Middlewares;

public sealed class OrgContextEnforcementMiddleware
{
    private readonly RequestDelegate _next;

    public OrgContextEnforcementMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task Invoke(HttpContext context, IOrgContext orgContext)
    {
        if (!IsProtectedOrgRoute(context))
        {
            await _next(context);
            return;
        }

        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        if (orgContext.IsRequestedOrganizacaoHeaderInvalid())
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(
                message: "Header X-Organizacao-Id invalido.",
                code: "ORG_HEADER_INVALID"));
            return;
        }

        if (orgContext.IsRequestedOrganizacaoHeaderProvided())
        {
            var idHeader = orgContext.GetRequestedOrganizacaoId();
            if (!idHeader.HasValue || !await orgContext.HasAccessToOrganizacaoAsync(idHeader.Value, context.RequestAborted))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(
                    message: "Usuario sem acesso a organizacao informada no header.",
                    code: "ORG_FORBIDDEN"));
                return;
            }
        }

        await _next(context);
    }

    private static bool IsProtectedOrgRoute(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        return path.StartsWith("/api/organizacao", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/estoque", StringComparison.OrdinalIgnoreCase);
    }
}
