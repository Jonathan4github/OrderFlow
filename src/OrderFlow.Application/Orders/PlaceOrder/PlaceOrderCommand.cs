using MediatR;

namespace OrderFlow.Application.Orders.PlaceOrder;

/// <summary>
/// Command dispatched to place a new order for a customer.
/// </summary>
/// <param name="CustomerId">Identifier of the customer placing the order.</param>
/// <param name="Items">Products and quantities to order (must be non-empty).</param>
public sealed record PlaceOrderCommand(
    Guid CustomerId,
    IReadOnlyCollection<PlaceOrderItem> Items) : IRequest<PlaceOrderResult>;

/// <summary>One line in a <see cref="PlaceOrderCommand"/>.</summary>
public sealed record PlaceOrderItem(Guid ProductId, int Quantity);
