using OrderFlow.Domain.Orders;
using OrderFlow.Domain.Repositories;

namespace OrderFlow.Application.Abstractions.Persistence;

/// <summary>Persistence port for the <see cref="Order"/> aggregate.</summary>
public interface IOrderRepository : IRepository<Order>
{
    /// <summary>Loads an order with its line items.</summary>
    Task<Order?> GetWithItemsAsync(Guid orderId, CancellationToken cancellationToken = default);
}
