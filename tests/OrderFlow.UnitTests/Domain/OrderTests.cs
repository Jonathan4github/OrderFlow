using FluentAssertions;
using OrderFlow.Domain.Enums;
using OrderFlow.Domain.Events;
using OrderFlow.Domain.Exceptions;
using OrderFlow.Domain.Orders;
using OrderFlow.Domain.Products;
using OrderFlow.Domain.ValueObjects;

namespace OrderFlow.UnitTests.Domain;

public class OrderTests
{
    private static Product NewProduct(string name = "Widget", decimal price = 10m) =>
        new(Guid.NewGuid(), name, new Money(price));

    [Fact]
    public void Place_creates_pending_order_and_raises_OrderPlaced()
    {
        var product = NewProduct(price: 12.50m);

        var order = Order.Place(Guid.NewGuid(), [(product, 2)]);

        order.Status.Should().Be(OrderStatus.Pending);
        order.Items.Should().HaveCount(1);
        order.TotalAmount.Amount.Should().Be(25m);
        order.DomainEvents.Should().ContainSingle(e => e is OrderPlacedDomainEvent);
    }

    [Fact]
    public void Place_rejects_empty_line_items()
    {
        var act = () => Order.Place(Guid.NewGuid(), []);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Place_rejects_inactive_product()
    {
        var product = NewProduct();
        product.Deactivate();

        var act = () => Order.Place(Guid.NewGuid(), [(product, 1)]);

        act.Should().Throw<InvalidOrderStateException>();
    }

    [Fact]
    public void TotalAmount_sums_multiple_line_items()
    {
        var a = NewProduct(price: 10m);
        var b = NewProduct(price: 2.5m);

        var order = Order.Place(Guid.NewGuid(), [(a, 1), (b, 4)]);

        order.TotalAmount.Amount.Should().Be(20m);
    }

    [Fact]
    public void MarkPaymentSucceeded_keeps_pending_and_raises_PaymentProcessed()
    {
        var order = Order.Place(Guid.NewGuid(), [(NewProduct(), 1)]);
        order.ClearDomainEvents();

        order.MarkPaymentSucceeded();

        order.Status.Should().Be(OrderStatus.Pending);
        order.DomainEvents
            .OfType<PaymentProcessedDomainEvent>()
            .Should().ContainSingle(e => e.IsSuccessful);
    }

    [Fact]
    public void MarkPaymentFailed_transitions_to_Failed()
    {
        var order = Order.Place(Guid.NewGuid(), [(NewProduct(), 1)]);

        order.MarkPaymentFailed("card declined");

        order.Status.Should().Be(OrderStatus.Failed);
        order.FailureReason.Should().Be("card declined");
        order.DomainEvents
            .OfType<PaymentProcessedDomainEvent>()
            .Should().ContainSingle(e => !e.IsSuccessful);
    }

    [Fact]
    public void Confirm_transitions_to_Confirmed_and_raises_InventoryConfirmed()
    {
        var order = Order.Place(Guid.NewGuid(), [(NewProduct(), 1)]);

        order.Confirm();

        order.Status.Should().Be(OrderStatus.Confirmed);
        order.ConfirmedAt.Should().NotBeNull();
        order.DomainEvents.Should().Contain(e => e is InventoryConfirmedDomainEvent);
    }

    [Fact]
    public void Confirm_rejects_when_not_pending()
    {
        var order = Order.Place(Guid.NewGuid(), [(NewProduct(), 1)]);
        order.MarkPaymentFailed("network");

        var act = () => order.Confirm();

        act.Should().Throw<InvalidOrderStateException>();
    }

    [Fact]
    public void Cancel_rejects_confirmed_order()
    {
        var order = Order.Place(Guid.NewGuid(), [(NewProduct(), 1)]);
        order.Confirm();

        var act = () => order.Cancel("changed mind");

        act.Should().Throw<InvalidOrderStateException>();
    }
}
