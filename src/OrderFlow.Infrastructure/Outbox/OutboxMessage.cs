namespace OrderFlow.Infrastructure.Outbox;

/// <summary>
/// Durable record of a domain event waiting to be published.
/// Written transactionally alongside the business change that raised it,
/// then dispatched asynchronously by <see cref="OutboxPublisherService"/>.
/// </summary>
public sealed class OutboxMessage
{
    /// <summary>Row identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Assembly-qualified CLR type name of the domain event payload.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>JSON-serialised payload.</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the message was enqueued.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>UTC timestamp when the message was successfully dispatched; null while pending.</summary>
    public DateTimeOffset? ProcessedAt { get; set; }

    /// <summary>Number of publish attempts made so far.</summary>
    public int AttemptCount { get; set; }

    /// <summary>Error text captured on the most recent failed attempt.</summary>
    public string? Error { get; set; }
}
