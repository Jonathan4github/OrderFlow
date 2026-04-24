namespace OrderFlow.Application.Abstractions.Persistence;

/// <summary>
/// Coordinates commits and transactional scope across repositories.
/// Concrete implementation lives in the infrastructure layer.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Persists all pending changes from tracked aggregates.</summary>
    /// <returns>The number of state entries written.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the supplied action inside a database transaction.
    /// Implementations are responsible for wiring the appropriate isolation level
    /// and rolling back on exceptions.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default);
}
