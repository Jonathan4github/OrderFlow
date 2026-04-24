using OrderFlow.Domain.Common;
using OrderFlow.Domain.ValueObjects;

namespace OrderFlow.Domain.Events;

/// <summary>
/// Raised after a payment attempt completes (successfully or otherwise).
/// Triggers inventory confirmation on success or a rollback on failure.
/// </summary>
public sealed record PaymentProcessedDomainEvent(
    Guid OrderId,
    Guid CustomerId,
    Money Amount,
    bool IsSuccessful,
    string? FailureReason) : DomainEvent;
