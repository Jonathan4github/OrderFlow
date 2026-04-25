using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderFlow.API.Contracts.Orders;
using OrderFlow.Application.Orders.PlaceOrder;
using OrderFlow.IntegrationTests.Infrastructure;

namespace OrderFlow.IntegrationTests.Orders;

[Collection("postgres")]
public sealed class IdempotencyTests : IntegrationTestBase
{
    private static readonly Guid ProductId = new("33333333-3333-3333-3333-333333333333");
    private static readonly Guid CustomerId = new("44444444-4444-4444-4444-444444444444");

    public IdempotencyTests(PostgresContainerFixture postgres) : base(postgres) { }

    [Fact]
    public async Task Same_key_with_same_body_serves_cached_response_and_runs_handler_once()
    {
        await ResetWithSeedAsync((ProductId, "Cached", 19.99m, 10));

        var key = Guid.NewGuid().ToString("N");
        var request = new PlaceOrderRequest(
            CustomerId,
            [new PlaceOrderRequestItem(ProductId, 2)]);

        var first = await SendWithKeyAsync(request, key);
        var second = await SendWithKeyAsync(request, key);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created);

        var firstBody = await ReadJsonAsync<PlaceOrderResult>(first);
        var secondBody = await ReadJsonAsync<PlaceOrderResult>(second);
        secondBody!.OrderId.Should().Be(firstBody!.OrderId,
            "the second call must be served from the idempotency cache");

        await using var scope = CreateDbScope();
        var db = Db(scope);
        (await db.Orders.AsNoTracking().CountAsync()).Should()
            .Be(1, "only the first call may execute the command handler");

        var inventory = await db.Inventories.AsNoTracking().FirstAsync(i => i.Id == ProductId);
        inventory.QuantityOnHand.Should().Be(8); // only one reservation of 2
        inventory.QuantityReserved.Should().Be(2);
    }

    [Fact]
    public async Task Same_key_with_different_body_returns_409()
    {
        await ResetWithSeedAsync((ProductId, "Cached", 19.99m, 10));

        var key = Guid.NewGuid().ToString("N");

        var first = await SendWithKeyAsync(
            new PlaceOrderRequest(CustomerId, [new PlaceOrderRequestItem(ProductId, 1)]),
            key);
        var second = await SendWithKeyAsync(
            new PlaceOrderRequest(CustomerId, [new PlaceOrderRequestItem(ProductId, 3)]),
            key);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private async Task<HttpResponseMessage> SendWithKeyAsync(PlaceOrderRequest request, string key)
    {
        using var msg = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(request)
        };
        msg.Headers.Add("Idempotency-Key", key);
        return await Client.SendAsync(msg);
    }
}
