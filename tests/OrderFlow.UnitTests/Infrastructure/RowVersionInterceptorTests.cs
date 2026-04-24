using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Domain.Inventories;
using OrderFlow.Domain.Products;
using OrderFlow.Domain.ValueObjects;
using OrderFlow.Infrastructure.Persistence;
using OrderFlow.Infrastructure.Persistence.Interceptors;

namespace OrderFlow.UnitTests.Infrastructure;

public class RowVersionInterceptorTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public void Increments_row_version_when_inventory_is_added()
    {
        using var db = CreateContext();
        var inventory = new Inventory(Guid.NewGuid(), initialQuantity: 10);
        db.Inventories.Add(inventory);

        RowVersionInterceptor.BumpRowVersions(db);

        inventory.RowVersion.Should().Be(1u);
    }

    [Fact]
    public void Increments_row_version_when_inventory_is_modified()
    {
        using var db = CreateContext();
        var inventory = new Inventory(Guid.NewGuid(), initialQuantity: 10);
        db.Inventories.Add(inventory);
        db.ChangeTracker.Entries<Inventory>().Single().State = EntityState.Unchanged;

        inventory.Reserve(3);
        db.ChangeTracker.Entries<Inventory>().Single().State = EntityState.Modified;

        RowVersionInterceptor.BumpRowVersions(db);

        inventory.RowVersion.Should().Be(1u);
    }

    [Fact]
    public void Does_not_touch_unchanged_inventory_entries()
    {
        using var db = CreateContext();
        var inventory = new Inventory(Guid.NewGuid(), initialQuantity: 10);
        db.Inventories.Add(inventory);
        db.ChangeTracker.Entries<Inventory>().Single().State = EntityState.Unchanged;

        RowVersionInterceptor.BumpRowVersions(db);

        inventory.RowVersion.Should().Be(0u);
    }

    [Fact]
    public void Ignores_entities_of_other_types()
    {
        using var db = CreateContext();
        var product = new Product(Guid.NewGuid(), "Widget", new Money(10m));
        db.Products.Add(product);

        RowVersionInterceptor.BumpRowVersions(db);
        // No Inventory entries → nothing to bump. Just asserting we didn't throw.
        db.ChangeTracker.Entries<Inventory>().Should().BeEmpty();
    }
}
