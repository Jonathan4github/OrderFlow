namespace OrderFlow.Infrastructure.Idempotency;

/// <summary>
/// Durable cache entry for an <c>Idempotency-Key</c> header. Written after
/// the first successful request completes; read on every subsequent request
/// carrying the same key.
/// </summary>
public sealed class IdempotencyRecord
{
    /// <summary>Client-supplied idempotency key (primary key).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Hash of the normalised request body. Guards against a malicious or
    /// buggy client replaying the same key with different content and
    /// receiving the original response.
    /// </summary>
    public string RequestHash { get; set; } = string.Empty;

    /// <summary>HTTP status code of the cached response.</summary>
    public int StatusCode { get; set; }

    /// <summary>Cached response's Content-Type (if any).</summary>
    public string? ContentType { get; set; }

    /// <summary>Raw response body bytes.</summary>
    public byte[] Body { get; set; } = [];

    /// <summary>UTC timestamp when the record was written.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>UTC timestamp after which the record may be deleted.</summary>
    public DateTimeOffset ExpiresAt { get; set; }
}
