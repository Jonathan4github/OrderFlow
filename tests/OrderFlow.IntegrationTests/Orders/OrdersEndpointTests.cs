using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderFlow.API.Contracts.Orders;
using OrderFlow.Application.Orders.PlaceOrder;
using OrderFlow.Domain.Enums;
using OrderFlow.IntegrationTests.Infrastructure;

namespace OrderFlow.IntegrationTests.Orders;

/// <summary>
/// HTTP-level coverage for <c>POST /api/orders</c>. The four scenarios cover
/// the API's three exception → status mappings plus the happy path.
/// </summary>
[Collection("postgres")]
public sealed class OrdersEndpointTests : IntegrationTestBase
{
    private static readonly Guid WidgetId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CustomerId = new("22222222-2222-2222-2222-222222222222");

    public OrdersEndpointTests(PostgresContainerFixture postgres) : base(postgres) { }

    [Fact]
    public async Task Happy_path_returns_201_and_persists_pending_order()
    {
        await ResetWithSeedAsync((WidgetId, "Widget", 12.50m, 5));

        var request = new PlaceOrderRequest(
            CustomerId,
            [new PlaceOrderRequestItem(WidgetId, 2)]);

        var response = await Client.PostAsJsonAsync("/api/orders", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync<PlaceOrderResult>(response);
        body.Should().NotBeNull();
        body!.Status.Should().Be(nameof(OrderStatus.Pending));
        body.TotalAmount.Should().Be(25m);
        body.Currency.Should().Be("USD");
        response.Headers.Location.Should().NotBeNull();

        await using var scope = CreateDbScope();
        var db = Db(scope);
        var savedOrder = await db.Orders.AsNoTracking().Include(o => o.Items)
            .FirstAsync(o => o.Id == body.OrderId);
        savedOrder.Items.Should().HaveCount(1);

        var inventory = await db.Inventories.AsNoTracking().FirstAsync(i => i.Id == WidgetId);
        inventory.QuantityOnHand.Should().Be(3);
        inventory.QuantityReserved.Should().Be(2);
    }

    [Fact]
    public async Task Insufficient_stock_returns_409_with_diagnostic_extras()
    {
        await ResetWithSeedAsync((WidgetId, "Widget", 12.50m, 1));

        var request = new PlaceOrderRequest(
            CustomerId,
            [new PlaceOrderRequestItem(WidgetId, 5)]);

        var response = await Client.PostAsJsonAsync("/api/orders", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsExtras>();
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("Insufficient stock");
        problem.Extensions.Should().ContainKey("requested");
        problem.Extensions["requested"].GetInt32().Should().Be(5);
        problem.Extensions["available"].GetInt32().Should().Be(1);

        await using var scope = CreateDbScope();
        var db = Db(scope);
        (await db.Orders.AsNoTracking().AnyAsync()).Should().BeFalse();
        var inventory = await db.Inventories.AsNoTracking().FirstAsync(i => i.Id == WidgetId);
        inventory.QuantityOnHand.Should().Be(1);
        inventory.QuantityReserved.Should().Be(0);
    }

    [Fact]
    public async Task Product_not_found_returns_422()
    {
        await ResetWithSeedAsync((WidgetId, "Widget", 12.50m, 5));

        var unknown = Guid.NewGuid();
        var request = new PlaceOrderRequest(
            CustomerId,
            [new PlaceOrderRequestItem(unknown, 1)]);

        var response = await Client.PostAsJsonAsync("/api/orders", request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsExtras>();
        problem!.Title.Should().Be("Product not found");
        problem.Extensions["productId"].GetString().Should().Be(unknown.ToString());
    }

    [Fact]
    public async Task Validation_failure_returns_400_with_errors_dictionary()
    {
        await ResetWithSeedAsync((WidgetId, "Widget", 12.50m, 5));

        var request = new PlaceOrderRequest(CustomerId, []);

        var response = await Client.PostAsJsonAsync("/api/orders", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsExtras>();
        problem!.Title.Should().Be("Validation failed");
        problem.Extensions.Should().ContainKey("errors");
    }
}