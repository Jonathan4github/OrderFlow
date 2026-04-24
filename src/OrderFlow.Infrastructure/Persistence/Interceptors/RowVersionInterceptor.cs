using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OrderFlow.Domain.Inventories;

namespace OrderFlow.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Bumps <see cref="Inventory.RowVersion"/> on every modified
/// <see cref="Inventory"/> tracked by the context immediately before
/// <c>SaveChanges</c>. Combined with the <c>IsConcurrencyToken</c> mapping this
/// turns any concurrent update that bypassed the pessimistic <c>FOR UPDATE</c>
/// lock into a <see cref="DbUpdateConcurrencyException"/> at commit time.
/// </summary>
public sealed class RowVersionInterceptor : SaveChangesInterceptor
{
    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            BumpRowVersions(eventData.Context);
        }
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            BumpRowVersions(eventData.Context);
        }
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    internal static void BumpRowVersions(DbContext context)
    {
        foreach (var entry in context.ChangeTracker.Entries<Inventory>())
        {
            if (entry.State is EntityState.Modified or EntityState.Added)
            {
                IncrementRowVersion(entry);
            }
        }
    }

    private static void IncrementRowVersion(EntityEntry<Inventory> entry)
    {
        var rowVersion = entry.Property(nameof(Inventory.RowVersion));
        var current = (uint)(rowVersion.CurrentValue ?? 0u);
        rowVersion.CurrentValue = current + 1u;
    }
}