using Microsoft.EntityFrameworkCore;
using OrderFlow.Domain.Common;
using OrderFlow.Domain.Repositories;
using OrderFlow.Infrastructure.Persistence;

namespace OrderFlow.Infrastructure.Repositories;

/// <summary>Shared EF-Core-backed implementation of <see cref="IRepository{T}"/>.</summary>
public abstract class Repository<T>(AppDbContext db) : IRepository<T> where T : AggregateRoot
{
    /// <summary>Shared database context.</summary>
    protected AppDbContext Db { get; } = db;

    /// <summary>DbSet for the aggregate type.</summary>
    protected DbSet<T> Set => Db.Set<T>();

    /// <inheritdoc />
    public virtual Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    /// <inheritdoc />
    public virtual async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await Set.AddAsync(entity, cancellationToken);
    }

    /// <inheritdoc />
    public virtual void Update(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        Set.Update(entity);
    }

    /// <inheritdoc />
    public virtual void Remove(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        Set.Remove(entity);
    }
}
