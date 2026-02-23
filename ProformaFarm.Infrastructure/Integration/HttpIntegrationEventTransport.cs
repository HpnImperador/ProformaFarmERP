using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProformaFarm.Infrastructure.Integration;

public sealed class HttpIntegrationEventTransport : IIntegrationEventTransport
{
    private readonly HttpClient _httpClient;

    public HttpIntegrationEventTransport(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IntegrationTransportResult> SendAsync(IntegrationTransportRequest request, CancellationToken cancellationToken)
    {
        if (Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) &&
            string.Equals(uri.Scheme, "mock", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(uri.Host, "success", StringComparison.OrdinalIgnoreCase))
            {
                return new IntegrationTransportResult
                {
                    Success = true,
                    StatusCode = 200,
                    ResponseBody = "mock-success"
                };
            }

            return new IntegrationTransportResult
            {
                Success = false,
                StatusCode = 503,
                ErrorMessage = "mock-failure"
            };
        }

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, request.Url)
        {
            Content = new StringContent(request.Payload, Encoding.UTF8, "application/json")
        };

        requestMessage.Headers.TryAddWithoutValidation("X-Event-Type", request.EventType);
        if (!string.IsNullOrWhiteSpace(request.CorrelationId))
            requestMessage.Headers.TryAddWithoutValidation("X-Correlation-Id", request.CorrelationId);
        if (!string.IsNullOrWhiteSpace(request.SignatureHeaderName) && !string.IsNullOrWhiteSpace(request.SignatureValue))
            requestMessage.Headers.TryAddWithoutValidation(request.SignatureHeaderName, request.SignatureValue);

        try
        {
            using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return new IntegrationTransportResult
            {
                Success = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                ResponseBody = string.IsNullOrWhiteSpace(body) ? null : body,
                ErrorMessage = response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            return new IntegrationTransportResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
