using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderFlow.API.Contracts.Orders;
using OrderFlow.IntegrationTests.Infrastructure;

namespace OrderFlow.IntegrationTests.Orders;

/// <summary>
/// The assessment's marquee concurrency requirement: 50 simultaneous orders for
/// the last unit must result in exactly one success — and the inventory state
/// after must reflect a single reservation.
/// </summary>
[Collection("postgres")]
public sealed class ConcurrentReservationTests : IntegrationTestBase
{
    private const int Contenders = 50;
    private static readonly Guid ScarceProductId = new("55555555-5555-5555-5555-555555555555");

    public ConcurrentReservationTests(PostgresContainerFixture postgres) : base(postgres) { }

    [Fact]
    public async Task Fifty_concurrent_orders_for_stock_of_one_yield_exactly_one_201()
    {
        await ResetWithSeedAsync((ScarceProductId, "Last One", 99.99m, 1));

        var tasks = Enumerable.Range(0, Contenders)
            .Select(_ => Task.Run(() => Client.PostAsJsonAsync(
                "/api/orders",
                new PlaceOrderRequest(
                    Guid.NewGuid(),
                    [new PlaceOrderRequestItem(ScarceProductId, 1)]))))
            .ToArray();

        var responses = await Task.WhenAll(tasks);
        var statusCounts = responses
            .GroupBy(r => r.StatusCode)
            .ToDictionary(g => g.Key, g => g.Count());

        statusCounts.GetValueOrDefault(HttpStatusCode.Created)
            .Should().Be(1, "exactly one request must win the reservation");

        statusCounts.GetValueOrDefault(HttpStatusCode.Conflict)
            .Should().Be(Contenders - 1,
                "every other request must observe an InsufficientStockException");

        // Final DB state proves no over-sell occurred.
        await using var scope = CreateDbScope();
        var db = Db(scope);

        (await db.Orders.AsNoTracking().CountAsync()).Should().Be(1);

        var inventory = await db.Inventories.AsNoTracking()
            .FirstAsync(i => i.Id == ScarceProductId);
        inventory.QuantityOnHand.Should().Be(0);
        inventory.QuantityReserved.Should().Be(1);

        // Cleanup: dispose responses so HttpClient connections release.
        foreach (var r in responses) r.Dispose();
    }
}
