using FluentAssertions;
using OrderFlow.Domain.ValueObjects;

namespace OrderFlow.UnitTests.Domain;

public class MoneyTests
{
    [Fact]
    public void Constructor_rounds_amount_to_two_decimals()
    {
        var money = new Money(10.125m);

        money.Amount.Should().Be(10.12m); // banker's rounding
        money.Currency.Should().Be("USD");
    }

    [Fact]
    public void Constructor_rejects_negative_amount()
    {
        var act = () => new Money(-0.01m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("USDX")]
    public void Constructor_rejects_invalid_currency(string code)
    {
        var act = () => new Money(1m, code);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Addition_same_currency_adds_amounts()
    {
        var total = new Money(5m) + new Money(3.5m);
        total.Amount.Should().Be(8.5m);
    }

    [Fact]
    public void Addition_different_currency_throws()
    {
        var act = () => new Money(5m, "USD") + new Money(3m, "EUR");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Multiplication_by_scalar_works_both_sides()
    {
        (new Money(2.5m) * 3).Amount.Should().Be(7.5m);
        (3 * new Money(2.5m)).Amount.Should().Be(7.5m);
    }

    [Fact]
    public void Multiplication_by_negative_throws()
    {
        var act = () => new Money(1m) * -1;
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
