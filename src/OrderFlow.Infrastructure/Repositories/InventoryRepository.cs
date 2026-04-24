using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Domain.Inventories;
using OrderFlow.Infrastructure.Persistence;

namespace OrderFlow.Infrastructure.Repositories;

/// <inheritdoc cref="IInventoryRepository" />
public sealed class InventoryRepository(AppDbContext db) : Repository<Inventory>(db), IInventoryRepository
{
    // ──────────────────────────────────────────────────────────────────────────
    // Why pessimistic locking for stock reservation?
    //
    // We chose `SELECT ... FOR UPDATE` over pure optimistic concurrency for the
    // reservation hot path because:
    //
    //   1. Under contention (N requests racing for the last unit), optimistic
    //      concurrency would let N-1 transactions do all their work and only
    //      fail at commit — wasting CPU, EF change-tracker churn, and log
    //      spam. With a row-level lock the losers queue and either succeed
    //      cheaply or fail fast via InsufficientStockException after the
    //      winner has committed.
    //   2. Stock reservations are short-lived writes that all target the same
    //      row (the inventory record for a product). This is exactly the
    //      workload pessimistic locking is designed for.
    //   3. Correctness is non-negotiable: we must never over-sell. FOR UPDATE
    //      provides a serialisation point that is simple to reason about.
    //
    // The RowVersion column on Inventory is kept as a secondary guard and is
    // wired in Step 6 (SELECT FOR UPDATE SKIP LOCKED + optimistic token) so
    // that if a different code path ever bypasses the lock, concurrent writes
    // still cannot silently stomp each other.
    // ──────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<Inventory?> GetForUpdateAsync(
        Guid productId, CancellationToken cancellationToken = default)
    {
        return await Set
            .FromSqlInterpolated(
                $"""SELECT * FROM "inventories" WHERE "ProductId" = {productId} FOR UPDATE""")
            .AsTracking()
            .FirstOrDefaultAsync(cancellationToken);
    }
}