using Microsoft.Extensions.Caching.Memory;

namespace OrderFlow.API.Middleware;

/// <summary>
/// Scaffold implementation of client-supplied idempotency keys.
/// Only <c>POST</c> requests carrying an <c>Idempotency-Key</c> header are
/// considered. The first response body and status for a given key are
/// cached in-process; subsequent requests with the same key return the
/// cached response immediately without re-invoking the handler.
/// </summary>
/// <remarks>
/// Step 8 replaces the <see cref="IMemoryCache"/> backing store with a
/// durable <c>idempotency_records</c> table so keys survive restarts and
/// work across multiple API instances.
/// </remarks>
public sealed class IdempotencyMiddleware(
    RequestDelegate next,
    IMemoryCache cache,
    ILogger<IdempotencyMiddleware> logger)
{
    private const string HeaderName = "Idempotency-Key";
    private static readonly TimeSpan TimeToLive = TimeSpan.FromMinutes(10);

    private readonly RequestDelegate _next = next;
    private readonly IMemoryCache _cache = cache;
    private readonly ILogger<IdempotencyMiddleware> _logger = logger;

    /// <summary>Middleware entry point.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsPost(context.Request.Method) ||
            !context.Request.Headers.TryGetValue(HeaderName, out var keyValues) ||
            string.IsNullOrWhiteSpace(keyValues.ToString()))
        {
            await _next(context);
            return;
        }

        var key = keyValues.ToString();
        var cacheKey = $"idempotency:{key}";

        if (_cache.TryGetValue<CachedResponse>(cacheKey, out var cached) && cached is not null)
        {
            _logger.LogInformation("Idempotency-Key {Key} served from cache", key);
            await WriteAsync(context, cached);
            return;
        }

        // Capture the downstream response so we can cache it.
        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await _next(context);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        buffer.Position = 0;
        var bytes = buffer.ToArray();

        // Only cache successful/deterministic responses so transient failures
        // are not pinned for the TTL window.
        if (context.Response.StatusCode is >= 200 and < 300)
        {
            var capture = new CachedResponse(
                context.Response.StatusCode,
                context.Response.ContentType,
                bytes);
            _cache.Set(cacheKey, capture, TimeToLive);
        }

        await context.Response.Body.WriteAsync(bytes);
    }

    private static async Task WriteAsync(HttpContext context, CachedResponse cached)
    {
        context.Response.StatusCode = cached.StatusCode;
        if (!string.IsNullOrEmpty(cached.ContentType))
        {
            context.Response.ContentType = cached.ContentType;
        }
        await context.Response.Body.WriteAsync(cached.Body);
    }

    private sealed record CachedResponse(int StatusCode, string? ContentType, byte[] Body);
}
