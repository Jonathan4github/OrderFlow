using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Orders.PlaceOrder;
using OrderFlow.Domain.Enums;
using OrderFlow.Domain.Events;
using OrderFlow.Domain.Exceptions;
using OrderFlow.Domain.Inventories;
using OrderFlow.Domain.Orders;
using OrderFlow.Domain.Products;
using OrderFlow.Domain.ValueObjects;

namespace OrderFlow.UnitTests.Application;

public class PlaceOrderCommandHandlerTests
{
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IInventoryRepository> _inventories = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    public PlaceOrderCommandHandlerTests()
    {
        _uow
            .Setup(u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<PlaceOrderResult>>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<PlaceOrderResult>>, CancellationToken>(
                (action, ct) => action(ct));
    }

    private PlaceOrderCommandHandler CreateSut() => new(
        _products.Object,
        _inventories.Object,
        _orders.Object,
        _uow.Object,
        NullLogger<PlaceOrderCommandHandler>.Instance);

    private static Product NewProduct(decimal price = 10m) =>
        new(Guid.NewGuid(), "Widget", new Money(price));

    [Fact]
    public async Task Places_order_and_reserves_stock_on_happy_path()
    {
        var product = NewProduct(price: 12.50m);
        var inventory = new Inventory(product.Id, initialQuantity: 5);

        _products.Setup(r => r.GetManyAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new[] { product });
        _inventories.Setup(r => r.GetForUpdateAsync(product.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(inventory);

        Order? captured = null;
        _orders.Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
               .Callback<Order, CancellationToken>((o, _) => captured = o)
               .Returns(Task.CompletedTask);

        var handler = CreateSut();
        var cmd = new PlaceOrderCommand(
            Guid.NewGuid(),
            [new PlaceOrderItem(product.Id, 2)]);

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.Status.Should().Be(nameof(OrderStatus.Pending));
        result.TotalAmount.Should().Be(25m);
        result.Currency.Should().Be("USD");

        captured.Should().NotBeNull();
        captured!.DomainEvents.Should().ContainSingle(e => e is OrderPlacedDomainEvent);

        inventory.QuantityOnHand.Should().Be(3);
        inventory.QuantityReserved.Should().Be(2);

        _inventories.Verify(r => r.Update(inventory), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Throws_ProductNotFoundException_when_product_is_missing()
    {
        var missingId = Guid.NewGuid();

        _products.Setup(r => r.GetManyAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Array.Empty<Product>());

        var handler = CreateSut();
        var cmd = new PlaceOrderCommand(Guid.NewGuid(), [new PlaceOrderItem(missingId, 1)]);

        var act = async () => await handler.Handle(cmd, CancellationToken.None);

        (await act.Should().ThrowAsync<ProductNotFoundException>())
            .Which.ProductId.Should().Be(missingId);
        _orders.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Throws_InsufficientStockException_when_reservation_exceeds_available()
    {
        var product = NewProduct();
        var inventory = new Inventory(product.Id, initialQuantity: 1);

        _products.Setup(r => r.GetManyAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new[] { product });
        _inventories.Setup(r => r.GetForUpdateAsync(product.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(inventory);

        var handler = CreateSut();
        var cmd = new PlaceOrderCommand(Guid.NewGuid(), [new PlaceOrderItem(product.Id, 5)]);

        var act = async () => await handler.Handle(cmd, CancellationToken.None);

        (await act.Should().ThrowAsync<InsufficientStockException>())
            .Which.Should().Match<InsufficientStockException>(e =>
                e.ProductId == product.Id && e.Requested == 5 && e.Available == 1);
        _orders.Verify(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Throws_InsufficientStockException_when_inventory_row_missing()
    {
        var product = NewProduct();

        _products.Setup(r => r.GetManyAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new[] { product });
        _inventories.Setup(r => r.GetForUpdateAsync(product.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((Inventory?)null);

        var handler = CreateSut();
        var cmd = new PlaceOrderCommand(Guid.NewGuid(), [new PlaceOrderItem(product.Id, 1)]);

        var act = async () => await handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<InsufficientStockException>();
    }
}
