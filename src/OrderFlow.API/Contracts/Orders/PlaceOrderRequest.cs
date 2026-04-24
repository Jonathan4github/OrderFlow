using OrderFlow.Application.Orders.PlaceOrder;

namespace OrderFlow.API.Contracts.Orders;

/// <summary>Request body for <c>POST /api/orders</c>.</summary>
public sealed record PlaceOrderRequest(
    Guid CustomerId,
    IReadOnlyList<PlaceOrderRequestItem> Items)
{
    /// <summary>Maps this DTO to the MediatR command.</summary>
    public PlaceOrderCommand ToCommand() =>
        new(CustomerId, Items.Select(i => new PlaceOrderItem(i.ProductId, i.Quantity)).ToArray());
}

/// <summary>One line in a <see cref="PlaceOrderRequest"/>.</summary>
public sealed record PlaceOrderRequestItem(Guid ProductId, int Quantity);
