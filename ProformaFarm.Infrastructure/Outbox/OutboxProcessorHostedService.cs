using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProformaFarm.Application.Interfaces.Outbox;

namespace ProformaFarm.Infrastructure.Outbox;

public sealed class OutboxProcessorHostedService : BackgroundService
{
    private readonly IOutboxProcessor _processor;
    private readonly OutboxProcessingOptions _options;
    private readonly ILogger<OutboxProcessorHostedService> _logger;

    public OutboxProcessorHostedService(
        IOutboxProcessor processor,
        IOptions<OutboxProcessingOptions> options,
        ILogger<OutboxProcessorHostedService> logger)
    {
        _processor = processor;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxProcessorHostedService iniciado.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _ = await _processor.ProcessPendingAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha no ciclo do OutboxProcessorHostedService.");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.PollingIntervalSeconds), stoppingToken);
        }
    }
}
