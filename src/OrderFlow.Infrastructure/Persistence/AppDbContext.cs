using Microsoft.EntityFrameworkCore;
using OrderFlow.Domain.Inventories;
using OrderFlow.Domain.Orders;
using OrderFlow.Domain.Products;
using OrderFlow.Infrastructure.Outbox;

namespace OrderFlow.Infrastructure.Persistence;

/// <summary>
/// Primary EF Core context for the OrderFlow database. Aggregates are loaded
/// through repository abstractions; direct <see cref="DbSet{TEntity}"/> access
/// is reserved for the outbox infrastructure and migrations.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <summary>Order aggregates.</summary>
    public DbSet<Order> Orders => Set<Order>();

    /// <summary>Product catalogue.</summary>
    public DbSet<Product> Products => Set<Product>();

    /// <summary>Per-product inventory records.</summary>
    public DbSet<Inventory> Inventories => Set<Inventory>();

    /// <summary>Outbox queue (infrastructure concern, not a domain aggregate).</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
