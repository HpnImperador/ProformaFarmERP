using System.Threading;
using System.Threading.Tasks;

namespace ProformaFarm.Infrastructure.Integration;

public interface IIntegrationEventTransport
{
    Task<IntegrationTransportResult> SendAsync(IntegrationTransportRequest request, CancellationToken cancellationToken);
}

public sealed class IntegrationTransportRequest
{
    public required string Url { get; init; }
    public required string EventType { get; init; }
    public required string Payload { get; init; }
    public string? SignatureHeaderName { get; init; }
    public string? SignatureValue { get; init; }
    public string? CorrelationId { get; init; }
}

public sealed class IntegrationTransportResult
{
    public required bool Success { get; init; }
    public int? StatusCode { get; init; }
    public string? ResponseBody { get; init; }
    public string? ErrorMessage { get; init; }
}
