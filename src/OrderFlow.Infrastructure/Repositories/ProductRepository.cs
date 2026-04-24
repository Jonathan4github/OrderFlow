using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Domain.Products;
using OrderFlow.Infrastructure.Persistence;

namespace OrderFlow.Infrastructure.Repositories;

/// <inheritdoc cref="IProductRepository" />
public sealed class ProductRepository(AppDbContext db) : Repository<Product>(db), IProductRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Product>> GetManyAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count == 0)
        {
            return Array.Empty<Product>();
        }

        return await Set
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(cancellationToken);
    }
}
