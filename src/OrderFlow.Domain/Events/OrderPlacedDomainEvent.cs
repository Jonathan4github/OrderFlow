using OrderFlow.Domain.Common;
using OrderFlow.Domain.ValueObjects;

namespace OrderFlow.Domain.Events;

/// <summary>
/// Raised when a customer successfully places an order and stock has been reserved.
/// Triggers the payment step in the downstream pipeline.
/// </summary>
public sealed record OrderPlacedDomainEvent(
    Guid OrderId,
    Guid CustomerId,
    IReadOnlyCollection<OrderItemSnapshot> Items,
    Money TotalAmount) : DomainEvent;
