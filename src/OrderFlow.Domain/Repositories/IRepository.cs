using OrderFlow.Domain.Common;

namespace OrderFlow.Domain.Repositories;

/// <summary>
/// Persistence contract shared by every aggregate-root repository.
/// Implementations live in the infrastructure layer. Calls do not commit
/// transactions — that responsibility belongs to <c>IUnitOfWork</c>.
/// </summary>
/// <typeparam name="T">The aggregate-root type.</typeparam>
public interface IRepository<T> where T : AggregateRoot
{
    /// <summary>Looks up an aggregate by its identifier.</summary>
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Inserts a new aggregate.</summary>
    Task AddAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>Marks an aggregate as modified.</summary>
    void Update(T entity);

    /// <summary>Marks an aggregate for deletion.</summary>
    void Remove(T entity);
}
