using MediatR;

namespace OrderFlow.Domain.Common;

/// <summary>
/// Base type for every domain event raised by an aggregate.
/// Implements <see cref="INotification"/> so events can be dispatched by MediatR
/// once they are picked up by the outbox publisher in the infrastructure layer.
/// </summary>
public abstract record DomainEvent : INotification
{
    /// <summary>Unique identifier of the event.</summary>
    public Guid EventId { get; } = Guid.NewGuid();

    /// <summary>UTC timestamp at which the event was raised.</summary>
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}
