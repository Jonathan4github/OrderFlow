namespace OrderFlow.API.Configuration;

/// <summary>
/// Bound from the <c>OrderFlow:Idempotency</c> configuration section.
/// </summary>
public sealed class IdempotencyOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "OrderFlow:Idempotency";

    /// <summary>How long a cached response remains valid. Defaults to 24 hours.</summary>
    public int RetentionHours { get; set; } = 24;

    /// <summary>Computed <see cref="TimeSpan"/> form of <see cref="RetentionHours"/>.</summary>
    public TimeSpan RetentionHoursTimeSpan => TimeSpan.FromHours(Math.Max(1, RetentionHours));
}
