namespace OrderFlow.Application.Orders.PlaceOrder;

/// <summary>
/// Response returned by <see cref="PlaceOrderCommandHandler"/>. Uses primitive
/// types so the API can serialise it without leaking domain value objects.
/// </summary>
public sealed record PlaceOrderResult(
    Guid OrderId,
    string Status,
    decimal TotalAmount,
    string Currency,
    DateTimeOffset PlacedAt);
