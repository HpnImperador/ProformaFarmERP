using Microsoft.AspNetCore.Http;

namespace ProformaFarm.API.Infrastructure.Correlation;

public static class CorrelationIdResolver
{
    private const string HeaderName = "x-correlation-id";

    public static string Resolve(HttpContext httpContext)
    {
        // 1) Preferir o header (se o seu middleware sempre escreve ele)
        if (httpContext.Response.Headers.TryGetValue(HeaderName, out var respHeader) && !string.IsNullOrWhiteSpace(respHeader))
            return respHeader.ToString();

        if (httpContext.Request.Headers.TryGetValue(HeaderName, out var reqHeader) && !string.IsNullOrWhiteSpace(reqHeader))
            return reqHeader.ToString();

        // 2) Fallback
        return httpContext.TraceIdentifier;
    }
}
