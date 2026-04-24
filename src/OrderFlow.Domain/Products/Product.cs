using OrderFlow.Domain.Common;
using OrderFlow.Domain.ValueObjects;

namespace OrderFlow.Domain.Products;

/// <summary>
/// Catalogue item a customer can order. Stock levels live on a separate
/// <see cref="Inventories.Inventory"/> aggregate keyed by <see cref="Entity.Id"/>.
/// </summary>
public sealed class Product : AggregateRoot
{
    /// <summary>Human-readable product name (snapshot copied onto order lines).</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Optional product description.</summary>
    public string? Description { get; private set; }

    /// <summary>Current catalogue price. Snapshotted onto order lines at placement.</summary>
    public Money Price { get; private set; } = Money.Zero();

    /// <summary>Whether the product is currently available for ordering.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>EF Core constructor.</summary>
    private Product()
    {
    }

    /// <summary>Creates a new product with the given attributes.</summary>
    public Product(Guid id, string name, Money price, string? description = null) : base(id)
    {
        Rename(name);
        ChangePrice(price);
        Description = description;
    }

    /// <summary>Updates the product name.</summary>
    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Product name must not be blank.", nameof(name));
        }

        Name = name.Trim();
    }

    /// <summary>Updates the catalogue price.</summary>
    public void ChangePrice(Money price)
    {
        ArgumentNullException.ThrowIfNull(price);
        Price = price;
    }

    /// <summary>Marks the product as not orderable without deleting the row.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Re-enables a previously deactivated product.</summary>
    public void Activate() => IsActive = true;
}
