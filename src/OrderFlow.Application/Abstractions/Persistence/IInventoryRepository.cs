using OrderFlow.Domain.Inventories;
using OrderFlow.Domain.Repositories;

namespace OrderFlow.Application.Abstractions.Persistence;

/// <summary>Persistence port for the <see cref="Inventory"/> aggregate.</summary>
public interface IInventoryRepository : IRepository<Inventory>
{
    /// <summary>
    /// Loads the inventory row for the given product and acquires a row-level
    /// lock (<c>SELECT ... FOR UPDATE</c>) for the current transaction. The caller
    /// must be running inside a unit-of-work transaction.
    /// </summary>
    Task<Inventory?> GetForUpdateAsync(
        Guid productId,
        CancellationToken cancellationToken = default);
}
