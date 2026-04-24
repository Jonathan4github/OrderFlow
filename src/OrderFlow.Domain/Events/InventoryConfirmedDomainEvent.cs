using OrderFlow.Domain.Common;

namespace OrderFlow.Domain.Events;

/// <summary>
/// Raised once stock reservations for a successfully paid order have been committed.
/// This is the terminal success signal used to drive customer notifications.
/// </summary>
public sealed record InventoryConfirmedDomainEvent(
    Guid OrderId,
    Guid CustomerId,
    IReadOnlyCollection<OrderItemSnapshot> Items) : DomainEvent;
