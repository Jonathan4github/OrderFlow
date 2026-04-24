using OrderFlow.Domain.ValueObjects;

namespace OrderFlow.Domain.Events;

/// <summary>
/// Immutable line-item projection used inside domain events. Decoupled from
/// <see cref="Orders.OrderItem"/> so event payloads never carry entity references.
/// </summary>
public sealed record OrderItemSnapshot(Guid ProductId, string ProductName, Money UnitPrice, int Quantity);