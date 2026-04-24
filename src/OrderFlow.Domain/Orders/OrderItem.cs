using OrderFlow.Domain.Common;
using OrderFlow.Domain.ValueObjects;

namespace OrderFlow.Domain.Orders;

/// <summary>
/// Line item belonging to an <see cref="Order"/>. Product name and unit price
/// are snapshotted at placement time to preserve historical accuracy even if
/// the catalogue entry changes later.
/// </summary>
public sealed class OrderItem : Entity
{
    /// <summary>Identifier of the parent order.</summary>
    public Guid OrderId { get; private set; }

    /// <summary>Identifier of the ordered product.</summary>
    public Guid ProductId { get; private set; }

    /// <summary>Product name at the time of order.</summary>
    public string ProductName { get; private set; } = string.Empty;

    /// <summary>Unit price charged (snapshot of product price at order time).</summary>
    public Money UnitPrice { get; private set; } = Money.Zero();

    /// <summary>Number of units ordered.</summary>
    public int Quantity { get; private set; }

    /// <summary>Line total = <see cref="UnitPrice"/> × <see cref="Quantity"/>.</summary>
    public Money LineTotal => UnitPrice * Quantity;

    /// <summary>EF Core constructor.</summary>
    private OrderItem()
    {
    }

    internal OrderItem(Guid id, Guid orderId, Guid productId, string productName, Money unitPrice, int quantity)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(productName))
        {
            throw new ArgumentException("Product name must not be blank.", nameof(productName));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be positive.");
        }

        ArgumentNullException.ThrowIfNull(unitPrice);

        OrderId = orderId;
        ProductId = productId;
        ProductName = productName;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }
}