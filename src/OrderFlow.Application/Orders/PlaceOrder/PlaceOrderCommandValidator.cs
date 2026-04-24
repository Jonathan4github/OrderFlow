using FluentValidation;

namespace OrderFlow.Application.Orders.PlaceOrder;

/// <summary>
/// Structural validation for <see cref="PlaceOrderCommand"/>. Product
/// existence and stock availability are enforced inside the handler
/// because they require a database lookup.
/// </summary>
public sealed class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
{
    /// <summary>Configures the validator rules.</summary>
    public PlaceOrderCommandValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("CustomerId is required.");

        RuleFor(x => x.Items)
            .NotNull().WithMessage("At least one item is required.")
            .Must(items => items is { Count: > 0 }).WithMessage("At least one item is required.");

        RuleFor(x => x.Items)
            .Must(HaveUniqueProducts!).WithMessage("Duplicate products are not allowed; combine quantities instead.")
            .When(x => x.Items is { Count: > 0 });

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId)
                .NotEmpty().WithMessage("ProductId is required.");

            item.RuleFor(i => i.Quantity)
                .GreaterThan(0).WithMessage("Quantity must be greater than zero.");
        });
    }

    private static bool HaveUniqueProducts(IReadOnlyCollection<PlaceOrderItem> items) =>
        items.Select(i => i.ProductId).Distinct().Count() == items.Count;
}
