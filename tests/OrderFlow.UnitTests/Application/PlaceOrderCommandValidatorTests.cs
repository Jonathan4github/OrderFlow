using FluentAssertions;
using FluentValidation.TestHelper;
using OrderFlow.Application.Orders.PlaceOrder;

namespace OrderFlow.UnitTests.Application;

public class PlaceOrderCommandValidatorTests
{
    private readonly PlaceOrderCommandValidator _sut = new();

    private static PlaceOrderCommand ValidCommand(params PlaceOrderItem[] items) =>
        new(Guid.NewGuid(), items.Length == 0
            ? [new PlaceOrderItem(Guid.NewGuid(), 1)]
            : items);

    [Fact]
    public void Rejects_empty_customer_id()
    {
        var cmd = ValidCommand() with { CustomerId = Guid.Empty };
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(c => c.CustomerId);
    }

    [Fact]
    public void Rejects_empty_item_list()
    {
        var cmd = new PlaceOrderCommand(Guid.NewGuid(), []);
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(c => c.Items);
    }

    [Fact]
    public void Rejects_zero_or_negative_quantity()
    {
        var cmd = ValidCommand(new PlaceOrderItem(Guid.NewGuid(), 0));
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor("Items[0].Quantity");
    }

    [Fact]
    public void Rejects_empty_product_id()
    {
        var cmd = ValidCommand(new PlaceOrderItem(Guid.Empty, 1));
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor("Items[0].ProductId");
    }

    [Fact]
    public void Rejects_duplicate_product_ids()
    {
        var productId = Guid.NewGuid();
        var cmd = ValidCommand(
            new PlaceOrderItem(productId, 1),
            new PlaceOrderItem(productId, 2));

        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(c => c.Items);
    }

    [Fact]
    public void Accepts_well_formed_command()
    {
        var cmd = new PlaceOrderCommand(
            Guid.NewGuid(),
            [new PlaceOrderItem(Guid.NewGuid(), 2), new PlaceOrderItem(Guid.NewGuid(), 1)]);

        _sut.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }
}
