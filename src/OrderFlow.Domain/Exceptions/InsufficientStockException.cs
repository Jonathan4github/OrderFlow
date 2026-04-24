namespace OrderFlow.Domain.Exceptions;

/// <summary>
/// Thrown when a stock reservation cannot be satisfied because the
/// available quantity on hand is lower than the requested amount.
/// </summary>
public sealed class InsufficientStockException : DomainException
{
    /// <summary>Product for which the reservation was attempted.</summary>
    public Guid ProductId { get; }

    /// <summary>Quantity that was requested.</summary>
    public int Requested { get; }

    /// <summary>Quantity that was actually available at the time of reservation.</summary>
    public int Available { get; }

    /// <summary>Creates a new <see cref="InsufficientStockException"/>.</summary>
    public InsufficientStockException(Guid productId, int requested, int available)
        : base($"Insufficient stock for product {productId}: requested {requested}, available {available}.")
    {
        ProductId = productId;
        Requested = requested;
        Available = available;
    }
}
