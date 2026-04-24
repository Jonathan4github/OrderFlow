using OrderFlow.Domain.Common;
using OrderFlow.Domain.Enums;
using OrderFlow.Domain.Events;
using OrderFlow.Domain.Exceptions;
using OrderFlow.Domain.Products;
using OrderFlow.Domain.ValueObjects;

namespace OrderFlow.Domain.Orders;

/// <summary>
/// Aggregate root for a customer order. Encapsulates its line items, status transitions,
/// and the domain events that drive the downstream pipeline (payment → inventory → notify).
/// </summary>
public sealed class Order : AggregateRoot
{
    private readonly List<OrderItem> _items = [];

    /// <summary>Identifier of the customer who placed the order.</summary>
    public Guid CustomerId { get; private set; }

    /// <summary>Current lifecycle status.</summary>
    public OrderStatus Status { get; private set; } = OrderStatus.Pending;

    /// <summary>Aggregate monetary total across all line items.</summary>
    public Money TotalAmount { get; private set; } = Money.Zero();

    /// <summary>UTC timestamp at which the order was placed.</summary>
    public DateTimeOffset PlacedAt { get; private set; }

    /// <summary>UTC timestamp at which the order reached its terminal successful state.</summary>
    public DateTimeOffset? ConfirmedAt { get; private set; }

    /// <summary>Reason for a <see cref="OrderStatus.Failed"/> or <see cref="OrderStatus.Cancelled"/> state.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>Read-only view over the line items.</summary>
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    /// <summary>EF Core constructor.</summary>
    private Order()
    {
    }

    private Order(Guid id, Guid customerId) : base(id)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("CustomerId must be supplied.", nameof(customerId));
        }

        CustomerId = customerId;
        PlacedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Places a new order for the supplied customer and line items, and raises an
    /// <see cref="OrderPlacedDomainEvent"/>. Stock reservation is performed separately
    /// by the command handler which has access to the inventory aggregates.
    /// </summary>
    public static Order Place(Guid customerId, IEnumerable<(Product Product, int Quantity)> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var order = new Order(Guid.NewGuid(), customerId);

        foreach (var (product, quantity) in lines)
        {
            order.AddItem(product, quantity);
        }

        if (order._items.Count == 0)
        {
            throw new ArgumentException("Order must have at least one line item.", nameof(lines));
        }

        order.RaiseDomainEvent(new OrderPlacedDomainEvent(
            order.Id,
            order.CustomerId,
            order._items
                .Select(i => new OrderItemSnapshot(i.ProductId, i.ProductName, i.UnitPrice, i.Quantity))
                .ToArray(),
            order.TotalAmount));

        return order;
    }

    private void AddItem(Product product, int quantity)
    {
        ArgumentNullException.ThrowIfNull(product);

        if (!product.IsActive)
        {
            throw new InvalidOrderStateException(
                $"Cannot order inactive product {product.Id} ({product.Name}).");
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be positive.");
        }

        var item = new OrderItem(
            id: Guid.NewGuid(),
            orderId: Id,
            productId: product.Id,
            productName: product.Name,
            unitPrice: product.Price,
            quantity: quantity);

        _items.Add(item);

        if (_items.Count == 1)
        {
            TotalAmount = item.LineTotal;
        }
        else if (TotalAmount.Currency != item.UnitPrice.Currency)
        {
            throw new InvalidOperationException("All order items must share the same currency.");
        }
        else
        {
            TotalAmount += item.LineTotal;
        }
    }

    /// <summary>
    /// Records a successful payment and raises a <see cref="PaymentProcessedDomainEvent"/>.
    /// </summary>
    public void MarkPaymentSucceeded()
    {
        EnsureStatus(OrderStatus.Pending, "Payment can only be recorded on a pending order.");

        RaiseDomainEvent(new PaymentProcessedDomainEvent(
            Id, CustomerId, TotalAmount, IsSuccessful: true, FailureReason: null));
    }

    /// <summary>
    /// Records a failed payment and transitions the order to <see cref="OrderStatus.Failed"/>.
    /// </summary>
    public void MarkPaymentFailed(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A failure reason is required.", nameof(reason));
        }

        EnsureStatus(OrderStatus.Pending, "Payment can only be recorded on a pending order.");

        Status = OrderStatus.Failed;
        FailureReason = reason;

        RaiseDomainEvent(new PaymentProcessedDomainEvent(
            Id, CustomerId, TotalAmount, IsSuccessful: false, FailureReason: reason));
    }

    /// <summary>
    /// Commits the inventory reservation and transitions the order to its
    /// terminal <see cref="OrderStatus.Confirmed"/> state, raising
    /// an <see cref="InventoryConfirmedDomainEvent"/>.
    /// </summary>
    public void Confirm()
    {
        EnsureStatus(OrderStatus.Pending, "Only a pending order can be confirmed.");

        Status = OrderStatus.Confirmed;
        ConfirmedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new InventoryConfirmedDomainEvent(
            Id,
            CustomerId,
            _items
                .Select(i => new OrderItemSnapshot(i.ProductId, i.ProductName, i.UnitPrice, i.Quantity))
                .ToArray()));
    }

    /// <summary>Cancels a pending order.</summary>
    public void Cancel(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A cancellation reason is required.", nameof(reason));
        }

        if (Status is OrderStatus.Confirmed or OrderStatus.Cancelled)
        {
            throw new InvalidOrderStateException(
                $"Cannot cancel an order in status '{Status}'.");
        }

        Status = OrderStatus.Cancelled;
        FailureReason = reason;
    }

    private void EnsureStatus(OrderStatus expected, string message)
    {
        if (Status != expected)
        {
            throw new InvalidOrderStateException($"{message} (current status: {Status}).");
        }
    }
}
