using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Domain.Orders;
using OrderFlow.Infrastructure.Persistence;

namespace OrderFlow.Infrastructure.Repositories;

/// <inheritdoc cref="IOrderRepository" />
public sealed class OrderRepository(AppDbContext db) : Repository<Order>(db), IOrderRepository
{
    /// <inheritdoc />
    public Task<Order?> GetWithItemsAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        Set.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
}
