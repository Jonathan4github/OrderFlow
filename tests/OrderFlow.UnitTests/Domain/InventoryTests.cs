using FluentAssertions;
using OrderFlow.Domain.Exceptions;
using OrderFlow.Domain.Inventories;

namespace OrderFlow.UnitTests.Domain;

public class InventoryTests
{
    [Fact]
    public void Reserve_moves_quantity_from_on_hand_to_reserved()
    {
        var inventory = new Inventory(Guid.NewGuid(), initialQuantity: 10);

        inventory.Reserve(3);

        inventory.QuantityOnHand.Should().Be(7);
        inventory.QuantityReserved.Should().Be(3);
    }

    [Fact]
    public void Reserve_throws_insufficient_stock_when_not_enough_on_hand()
    {
        var productId = Guid.NewGuid();
        var inventory = new Inventory(productId, initialQuantity: 2);

        var act = () => inventory.Reserve(5);

        act.Should()
            .Throw<InsufficientStockException>()
            .Which.Should().Match<InsufficientStockException>(e =>
                e.ProductId == productId && e.Requested == 5 && e.Available == 2);
    }

    [Fact]
    public void ConfirmReservation_clears_reserved_bucket()
    {
        var inventory = new Inventory(Guid.NewGuid(), initialQuantity: 10);
        inventory.Reserve(4);

        inventory.ConfirmReservation(4);

        inventory.QuantityOnHand.Should().Be(6);
        inventory.QuantityReserved.Should().Be(0);
    }

    [Fact]
    public void ReleaseReservation_returns_stock_to_on_hand()
    {
        var inventory = new Inventory(Guid.NewGuid(), initialQuantity: 10);
        inventory.Reserve(4);

        inventory.ReleaseReservation(4);

        inventory.QuantityOnHand.Should().Be(10);
        inventory.QuantityReserved.Should().Be(0);
    }

    [Fact]
    public void ConfirmReservation_rejects_more_than_reserved()
    {
        var inventory = new Inventory(Guid.NewGuid(), initialQuantity: 10);
        inventory.Reserve(2);

        var act = () => inventory.ConfirmReservation(5);

        act.Should().Throw<InvalidOperationException>();
    }
}
