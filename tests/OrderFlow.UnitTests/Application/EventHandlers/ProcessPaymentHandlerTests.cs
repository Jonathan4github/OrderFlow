using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrderFlow.Application.Abstractions.Payments;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Orders.EventHandlers;
using OrderFlow.Domain.Enums;
using OrderFlow.Domain.Events;
using OrderFlow.Domain.Orders;
using OrderFlow.Domain.Products;
using OrderFlow.Domain.ValueObjects;

namespace OrderFlow.UnitTests.Application.EventHandlers;

public class ProcessPaymentHandlerTests
{
    private readonly Mock<IPaymentGateway> _gateway = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private ProcessPaymentHandler CreateSut() => new(
        _gateway.Object,
        _orders.Object,
        _uow.Object,
        new TestResiliencePipelineProvider(),
        NullLogger<ProcessPaymentHandler>.Instance);

    private static Order NewPendingOrder()
    {
        var product = new Product(Guid.NewGuid(), "Widget", new Money(10m));
        return Order.Place(Guid.NewGuid(), [(product, 1)]);
    }

    [Fact]
    public async Task Marks_order_payment_succeeded_on_gateway_success()
    {
        var order = NewPendingOrder();
        _orders.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _gateway.Setup(g => g.ChargeAsync(order.Id, order.CustomerId, order.TotalAmount, It.IsAny<CancellationToken>()))
                .ReturnsAsync(PaymentResult.Success("txn_123"));

        await CreateSut().Handle(
            new OrderPlacedDomainEvent(order.Id, order.CustomerId, [], order.TotalAmount),
            CancellationToken.None);

        order.Status.Should().Be(OrderStatus.Pending);
        order.DomainEvents.OfType<PaymentProcessedDomainEvent>()
            .Should().ContainSingle(e => e.IsSuccessful);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Marks_order_payment_failed_on_gateway_failure()
    {
        var order = NewPendingOrder();
        _orders.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _gateway.Setup(g => g.ChargeAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Money>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PaymentResult.Failure("card declined"));

        await CreateSut().Handle(
            new OrderPlacedDomainEvent(order.Id, order.CustomerId, [], order.TotalAmount),
            CancellationToken.None);

        order.Status.Should().Be(OrderStatus.Failed);
        order.FailureReason.Should().Be("card declined");
        order.DomainEvents.OfType<PaymentProcessedDomainEvent>()
            .Should().ContainSingle(e => !e.IsSuccessful);
    }

    [Fact]
    public async Task Skips_when_order_missing()
    {
        _orders.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((Order?)null);

        await CreateSut().Handle(
            new OrderPlacedDomainEvent(Guid.NewGuid(), Guid.NewGuid(), [], new Money(1m)),
            CancellationToken.None);

        _gateway.Verify(
            g => g.ChargeAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Money>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Skips_when_order_already_past_pending()
    {
        var order = NewPendingOrder();
        order.MarkPaymentFailed("prior failure"); // order.Status = Failed
        _orders.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        await CreateSut().Handle(
            new OrderPlacedDomainEvent(order.Id, order.CustomerId, [], order.TotalAmount),
            CancellationToken.None);

        _gateway.Verify(
            g => g.ChargeAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Money>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
