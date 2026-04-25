namespace OrderFlow.Application.Abstractions.Idempotency;

/// <summary>
/// Durable backing store for client-supplied <c>Idempotency-Key</c> headers.
/// Each entry captures the outcome of a successful request so a duplicate
/// submission of the same key can be short-circuited without re-executing
/// the downstream pipeline.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>Looks up a cached response for the given key, honouring the stored TTL.</summary>
    Task<CachedResponse?> TryGetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the supplied response for the key with a caller-chosen TTL.
    /// Concurrent writes for the same key are tolerated — the first writer wins
    /// and subsequent calls are silent no-ops.
    /// </summary>
    Task SaveAsync(
        string key,
        CachedResponse response,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default);
}

/// <summary>Snapshot of an HTTP response cached for idempotency purposes.</summary>
public sealed record CachedResponse(
    int StatusCode,
    string? ContentType,
    byte[] Body,
    string RequestHash);