using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Abstractions.Idempotency;
using OrderFlow.Infrastructure.Persistence;

namespace OrderFlow.Infrastructure.Idempotency;

/// <summary>EF Core-backed implementation of <see cref="IIdempotencyStore"/>.</summary>
public sealed class EfIdempotencyStore(AppDbContext db) : IIdempotencyStore
{
    private readonly AppDbContext _db = db;

    /// <inheritdoc />
    public async Task<CachedResponse?> TryGetAsync(
        string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var now = DateTimeOffset.UtcNow;
        var record = await _db.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Key == key && r.ExpiresAt > now, cancellationToken);

        return record is null
            ? null
            : new CachedResponse(record.StatusCode, record.ContentType, record.Body, record.RequestHash);
    }

    /// <inheritdoc />
    public async Task SaveAsync(
        string key,
        CachedResponse response,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(response);

        var now = DateTimeOffset.UtcNow;
        var record = new IdempotencyRecord
        {
            Key = key,
            RequestHash = response.RequestHash,
            StatusCode = response.StatusCode,
            ContentType = response.ContentType,
            Body = response.Body,
            CreatedAt = now,
            ExpiresAt = now + timeToLive
        };

        _db.IdempotencyRecords.Add(record);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent caller beat us to the same key. Their record wins;
            // ours is discarded. Detach so the tracker doesn't retry on next save.
            _db.Entry(record).State = EntityState.Detached;
        }
    }
}
