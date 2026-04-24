namespace OrderFlow.Infrastructure.Outbox;

/// <summary>Bound from the <c>OrderFlow:Outbox</c> configuration section.</summary>
public sealed class OutboxOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "OrderFlow:Outbox";

    /// <summary>How often the publisher wakes up to check for pending messages.</summary>
    public int PollingIntervalSeconds { get; set; } = 2;

    /// <summary>Maximum number of messages dispatched per poll iteration.</summary>
    public int BatchSize { get; set; } = 20;

    /// <summary>Maximum retry attempts before a message is considered permanently failed.</summary>
    public int MaxAttempts { get; set; } = 5;
}
