namespace OrderFlow.Domain.Exceptions;

/// <summary>
/// Thrown when a referenced product cannot be located in the catalogue.
/// </summary>
public sealed class ProductNotFoundException : DomainException
{
    /// <summary>Identifier of the product that could not be found.</summary>
    public Guid ProductId { get; }

    /// <summary>Creates a new <see cref="ProductNotFoundException"/>.</summary>
    public ProductNotFoundException(Guid productId)
        : base($"Product {productId} was not found.")
    {
        ProductId = productId;
    }
}
