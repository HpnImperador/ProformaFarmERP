namespace ProformaFarm.Infrastructure.Outbox;

public sealed class OutboxProcessingOptions
{
    public const string SectionName = "Outbox";

    public int BatchSize { get; set; } = 25;
    public int PollingIntervalSeconds { get; set; } = 5;
    public int LockSeconds { get; set; } = 30;
    public int MaxRetries { get; set; } = 5;
    public int RetryBaseDelaySeconds { get; set; } = 2;
}
