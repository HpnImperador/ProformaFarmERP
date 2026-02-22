using System;
using Microsoft.AspNetCore.Http;
using ProformaFarm.Application.Interfaces.Correlation;

namespace ProformaFarm.API.Infrastructure.Correlation;

public sealed class HttpCorrelationIdAccessor : ICorrelationIdAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCorrelationIdAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? GetCurrentCorrelationId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
            return null;

        var correlation = CorrelationIdResolver.Resolve(httpContext);
        return Guid.TryParse(correlation, out var parsed) ? parsed : null;
    }
}
