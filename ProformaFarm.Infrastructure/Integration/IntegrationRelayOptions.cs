namespace ProformaFarm.Infrastructure.Integration;

public sealed class IntegrationRelayOptions
{
    public const string SectionName = "IntegrationRelay";

    public int BatchSize { get; set; } = 25;
    public int PollingIntervalSeconds { get; set; } = 8;
    public int LockSeconds { get; set; } = 45;
    public int MaxRetries { get; set; } = 5;
    public int RetryBaseDelaySeconds { get; set; } = 3;
    public string SignatureHeaderName { get; set; } = "X-Proforma-Signature";
}
