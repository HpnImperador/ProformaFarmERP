namespace ProformaFarm.Infrastructure.Outbox;

public static class OutboxEventStatus
{
    public const byte Pending = 0;
    public const byte Processing = 1;
    public const byte Processed = 2;
    public const byte Failed = 3;
}
