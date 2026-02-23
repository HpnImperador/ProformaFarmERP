using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProformaFarm.Application.Interfaces.Integration;

namespace ProformaFarm.Infrastructure.Integration;

public sealed class EventRelayHostedService : BackgroundService
{
    private readonly IEventRelayProcessor _processor;
    private readonly IntegrationRelayOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<EventRelayHostedService> _logger;

    public EventRelayHostedService(
        IEventRelayProcessor processor,
        IOptions<IntegrationRelayOptions> options,
        IHostEnvironment environment,
        ILogger<EventRelayHostedService> logger)
    {
        _processor = processor;
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_environment.IsEnvironment("Testing"))
        {
            _logger.LogInformation("EventRelayHostedService desativado no ambiente Testing.");
            return;
        }

        _logger.LogInformation("EventRelayHostedService iniciado.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _ = await _processor.ProcessPendingAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha no ciclo do EventRelayHostedService.");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.PollingIntervalSeconds), stoppingToken);
        }
    }
}
