namespace OrderFlow.Domain.Common;

/// <summary>
/// Marks an entity as an aggregate root capable of raising domain events.
/// Events are collected on the aggregate and dispatched by the unit of work
/// after a successful persist.
/// </summary>
public abstract class AggregateRoot : Entity
{
    private readonly List<DomainEvent> _domainEvents = [];

    /// <inheritdoc cref="Entity()" />
    protected AggregateRoot()
    {
    }

    /// <inheritdoc cref="Entity(Guid)" />
    protected AggregateRoot(Guid id) : base(id)
    {
    }

    /// <summary>Domain events pending dispatch for this aggregate.</summary>
    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Records a new domain event to be dispatched after persistence.</summary>
    protected void RaiseDomainEvent(DomainEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        _domainEvents.Add(@event);
    }

    /// <summary>Clears the pending events once they have been dispatched.</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}
