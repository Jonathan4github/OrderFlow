using OrderFlow.Domain.Products;
using OrderFlow.Domain.Repositories;

namespace OrderFlow.Application.Abstractions.Persistence;

/// <summary>Persistence port for the <see cref="Product"/> aggregate.</summary>
public interface IProductRepository : IRepository<Product>
{
    /// <summary>Loads all products whose identifier is in <paramref name="ids"/> in a single round-trip.</summary>
    Task<IReadOnlyList<Product>> GetManyAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);
}
