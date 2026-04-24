using OrderFlow.Domain.Common;
using OrderFlow.Domain.Exceptions;

namespace OrderFlow.Domain.Inventories;

/// <summary>
/// Stock record for a single product. Keeps two counters:
/// <list type="bullet">
///   <item><description><see cref="QuantityOnHand"/> — physical stock still sellable.</description></item>
///   <item><description><see cref="QuantityReserved"/> — stock pre-allocated to in-flight orders.</description></item>
/// </list>
/// The reservation model lets the payment and confirmation steps roll back
/// a reservation without ever over-selling the physical stock.
/// </summary>
public sealed class Inventory : AggregateRoot
{
    /// <summary>Product this inventory row is for. Matches <see cref="Entity.Id"/>.</summary>
    public Guid ProductId { get; private set; }

    /// <summary>Units physically in stock that are still free to be reserved.</summary>
    public int QuantityOnHand { get; private set; }

    /// <summary>Units held by pending orders awaiting confirmation.</summary>
    public int QuantityReserved { get; private set; }

    /// <summary>Concurrency token used by EF Core for optimistic concurrency.</summary>
    public uint RowVersion { get; private set; }

    /// <summary>Units still available for new reservations.</summary>
    public int QuantityAvailable => QuantityOnHand;

    /// <summary>EF Core constructor.</summary>
    private Inventory()
    {
    }

    /// <summary>Creates an inventory row for a given product.</summary>
    public Inventory(Guid productId, int initialQuantity) : base(productId)
    {
        if (initialQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialQuantity), initialQuantity, "Initial quantity must be non-negative.");
        }

        ProductId = productId;
        QuantityOnHand = initialQuantity;
        QuantityReserved = 0;
    }

    /// <summary>
    /// Reserves <paramref name="quantity"/> units. Moves stock from
    /// <see cref="QuantityOnHand"/> into <see cref="QuantityReserved"/>.
    /// </summary>
    /// <exception cref="InsufficientStockException">
    /// Thrown when the available quantity is lower than the requested amount.
    /// </exception>
    public void Reserve(int quantity)
    {
        EnsurePositive(quantity);

        if (QuantityOnHand < quantity)
        {
            throw new InsufficientStockException(ProductId, quantity, QuantityOnHand);
        }

        QuantityOnHand -= quantity;
        QuantityReserved += quantity;
    }

    /// <summary>Commits a previously held reservation (stock leaves the warehouse).</summary>
    public void ConfirmReservation(int quantity)
    {
        EnsurePositive(quantity);

        if (QuantityReserved < quantity)
        {
            throw new InvalidOperationException(
                $"Cannot confirm {quantity} units for product {ProductId}: only {QuantityReserved} reserved.");
        }

        QuantityReserved -= quantity;
    }

    /// <summary>Returns previously reserved units back to the free pool.</summary>
    public void ReleaseReservation(int quantity)
    {
        EnsurePositive(quantity);

        if (QuantityReserved < quantity)
        {
            throw new InvalidOperationException(
                $"Cannot release {quantity} units for product {ProductId}: only {QuantityReserved} reserved.");
        }

        QuantityReserved -= quantity;
        QuantityOnHand += quantity;
    }

    /// <summary>Adjusts the on-hand quantity (e.g. restocking).</summary>
    public void Restock(int quantity)
    {
        EnsurePositive(quantity);
        QuantityOnHand += quantity;
    }

    private static void EnsurePositive(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be positive.");
        }
    }
}