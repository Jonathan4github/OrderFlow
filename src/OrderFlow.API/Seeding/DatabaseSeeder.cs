using Microsoft.EntityFrameworkCore;
using OrderFlow.Domain.Inventories;
using OrderFlow.Domain.Products;
using OrderFlow.Domain.ValueObjects;
using OrderFlow.Infrastructure.Persistence;

namespace OrderFlow.API.Seeding;

/// <summary>
/// Applies pending migrations and seeds a fixed set of demo products with
/// inventory so the API is usable end-to-end after a fresh <c>docker compose up</c>.
/// Safe to call repeatedly: every insert is predicated on the aggregate Id
/// not already existing.
/// </summary>
public static class DatabaseSeeder
{
    /// <summary>
    /// Five fixed-GUID products with stock, exposed so integration tests can
    /// reference the same identifiers.
    /// </summary>
    public static readonly IReadOnlyList<SeedProduct> SeedProducts =
    [
        new(new Guid("aaaaaaaa-0000-0000-0000-000000000001"), "Mechanical Keyboard", 129.99m, 50),
        new(new Guid("aaaaaaaa-0000-0000-0000-000000000002"), "Wireless Mouse",      49.50m,  100),
        new(new Guid("aaaaaaaa-0000-0000-0000-000000000003"), "USB-C Hub",           29.00m,  200),
        new(new Guid("aaaaaaaa-0000-0000-0000-000000000004"), "Noise-Cancel Headset", 219.00m, 25),
        new(new Guid("aaaaaaaa-0000-0000-0000-000000000005"), "27\" 4K Monitor",     399.00m, 10),
    ];

    /// <summary>Migrates to the latest schema and inserts missing seed rows.</summary>
    public static async Task MigrateAndSeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<SeedMarker>>();

        logger.LogInformation("Applying EF Core migrations");
        await db.Database.MigrateAsync(ct);

        var existingProductIds = await db.Products
            .Select(p => p.Id)
            .ToListAsync(ct);
        var existing = existingProductIds.ToHashSet();

        var productsToAdd = new List<Product>();
        var inventoriesToAdd = new List<Inventory>();

        foreach (var seed in SeedProducts)
        {
            if (existing.Contains(seed.Id))
            {
                continue;
            }

            productsToAdd.Add(new Product(seed.Id, seed.Name, new Money(seed.Price)));
            inventoriesToAdd.Add(new Inventory(seed.Id, seed.InitialStock));
        }

        if (productsToAdd.Count == 0)
        {
            logger.LogInformation("Seed data already present; skipping");
            return;
        }

        await db.Products.AddRangeAsync(productsToAdd, ct);
        await db.Inventories.AddRangeAsync(inventoriesToAdd, ct);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Seeded {ProductCount} product(s) with inventory totalling {TotalStock} units",
            productsToAdd.Count, inventoriesToAdd.Sum(i => i.QuantityOnHand));
    }

    /// <summary>Single seed product specification.</summary>
    public sealed record SeedProduct(Guid Id, string Name, decimal Price, int InitialStock);

    /// <summary>Category marker for logging scoped to the seeder.</summary>
    public sealed class SeedMarker;
}
