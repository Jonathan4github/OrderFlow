using System.Data;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Abstractions.Persistence;

namespace OrderFlow.Infrastructure.Persistence;

/// <summary>
/// EF-Core-backed implementation of <see cref="IUnitOfWork"/>. The transaction
/// opened by <see cref="ExecuteInTransactionAsync{T}"/> scopes every
/// <c>SELECT ... FOR UPDATE</c> acquired by the repositories, so either all
/// reservations succeed together or none is persisted.
/// </summary>
public sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    private readonly AppDbContext _db = db;

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);

    /// <inheritdoc />
    public Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_db.Database.CurrentTransaction is not null)
        {
            // Already inside an outer transaction (nested call) — just run the action.
            return action(cancellationToken);
        }

        // Execution strategy wraps the whole block so Npgsql's retry-on-failure
        // can retry the entire transaction as a unit on transient errors.
        var strategy = _db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(
            state: action,
            operation: async (_, act, ct) =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted, ct);
                try
                {
                    var result = await act(ct);
                    await transaction.CommitAsync(ct);
                    return result;
                }
                catch
                {
                    await transaction.RollbackAsync(ct);
                    throw;
                }
            },
            verifySucceeded: null,
            cancellationToken: cancellationToken);
    }
}
