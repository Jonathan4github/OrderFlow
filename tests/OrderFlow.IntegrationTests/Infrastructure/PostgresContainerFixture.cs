using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Domain.Inventories;
using OrderFlow.Domain.Products;
using OrderFlow.Domain.ValueObjects;
using OrderFlow.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace OrderFlow.IntegrationTests.Infrastructure;

/// <summary>
/// Spins up a single PostgreSQL 16 container for the lifetime of the test class.
/// EF migrations are applied once on startup. Per-test cleanup is the
/// responsibility of the calling test (see <c>ResetAsync</c> helper).
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("orderflow_test")
        .WithUsername("orderflow")
        .WithPassword("orderflow")
        .Build();

    /// <summary>Connection string the test factory feeds into the API.</summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <summary>Starts the container and applies migrations.</summary>
    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // Apply migrations once. The factory's CompositionRoot will reuse
        // the same database on every test that runs against this fixture.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
    }

    /// <summary>Tears the container down.</summary>
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>
    /// Wipes all rows from the application tables and reseeds the supplied products.
    /// Call from each test (or per-class fixture) so tests don't leak state.
    /// </summary>
    public async Task ResetAsync(IServiceProvider services, IEnumerable<(Guid Id, string Name, decimal Price, int Stock)> products)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.ExecuteSqlRawAsync("""
            TRUNCATE TABLE
                "order_items",
                "orders",
                "inventories",
                "products",
                "outbox_messages",
                "idempotency_records"
            RESTART IDENTITY CASCADE;
            """);

        foreach (var seed in products)
        {
            db.Products.Add(new Product(seed.Id, seed.Name, new Money(seed.Price)));
            db.Inventories.Add(new Inventory(seed.Id, seed.Stock));
        }

        await db.SaveChangesAsync();
    }
}
