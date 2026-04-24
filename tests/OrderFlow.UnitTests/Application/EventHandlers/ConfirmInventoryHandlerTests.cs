using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Orders.EventHandlers;
using OrderFlow.Domain.Enums;
using OrderFlow.Domain.Events;
using OrderFlow.Domain.Inventories;
using OrderFlow.Domain.Orders;
using OrderFlow.Domain.Products;
using OrderFlow.Domain.ValueObjects;

namespace OrderFlow.UnitTests.Application.EventHandlers;

public class ConfirmInventoryHandlerTests
{
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IInventoryRepository> _inventories = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private ConfirmInventoryHandler CreateSut() => new(
        _orders.Object,
        _inventories.Object,
        _uow.Object,
        new TestResiliencePipelineProvider(),
        NullLogger<ConfirmInventoryHandler>.Instance);

    private static (Order order, Inventory inventory, Product product) NewOrderWithStock(
        int initialOnHand, int quantity)
    {
        var product = new Product(Guid.NewGuid(), "Widget", new Money(10m));
        var inventory = new Inventory(product.Id, initialOnHand);
        inventory.Reserve(quantity);

        var order = Order.Place(Guid.NewGuid(), [(product, quantity)]);
        return (order, inventory, product);
    }

    [Fact]
    public async Task On_payment_success_confirms_each_reservation_and_confirms_order()
    {
        var (order, inventory, product) = NewOrderWithStock(initialOnHand: 10, quantity: 3);
        _orders.Setup(r => r.GetWithItemsAsync(order.Id, It.IsAny<CancellationToken>()))
               .ReturnsAsync(order);
        _inventories.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(inventory);

        await CreateSut().Handle(
            new PaymentProcessedDomainEvent(order.Id, order.CustomerId, order.TotalAmount,
                IsSuccessful: true, FailureReason: null),
            CancellationToken.None);

        order.Status.Should().Be(OrderStatus.Confirmed);
        order.DomainEvents.Should().Contain(e => e is InventoryConfirmedDomainEvent);

        inventory.QuantityReserved.Should().Be(0);
        inventory.QuantityOnHand.Should().Be(7);

        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task On_payment_failure_releases_reservations_and_leaves_order_failed()
    {
        var (order, inventory, product) = NewOrderWithStock(initialOnHand: 10, quantity: 4);
        order.MarkPaymentFailed("declined"); // sets Status = Failed

        _orders.Setup(r => r.GetWithItemsAsync(order.Id, It.IsAny<CancellationToken>()))
               .ReturnsAsync(order);
        _inventories.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(inventory);

        await CreateSut().Handle(
            new PaymentProcessedDomainEvent(order.Id, order.CustomerId, order.TotalAmount,
                IsSuccessful: false, FailureReason: "declined"),
            CancellationToken.None);

        order.Status.Should().Be(OrderStatus.Failed);
        inventory.QuantityReserved.Should().Be(0);
        inventory.QuantityOnHand.Should().Be(10);
        order.DomainEvents.OfType<InventoryConfirmedDomainEvent>().Should().BeEmpty();
    }

    [Fact]
    public async Task Skips_confirmation_when_order_already_confirmed()
    {
        var (order, _, _) = NewOrderWithStock(initialOnHand: 10, quantity: 1);
        order.Confirm(); // Status already Confirmed

        _orders.Setup(r => r.GetWithItemsAsync(order.Id, It.IsAny<CancellationToken>()))
               .ReturnsAsync(order);

        await CreateSut().Handle(
            new PaymentProcessedDomainEvent(order.Id, order.CustomerId, order.TotalAmount,
                IsSuccessful: true, FailureReason: null),
            CancellationToken.None);

        _inventories.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}