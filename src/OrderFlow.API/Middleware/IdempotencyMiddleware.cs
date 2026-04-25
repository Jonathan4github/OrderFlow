using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using OrderFlow.API.Configuration;
using OrderFlow.Application.Abstractions.Idempotency;

namespace OrderFlow.API.Middleware;

/// <summary>
/// Implements client-supplied idempotency on unsafe (POST/PUT/PATCH/DELETE)
/// requests carrying an <c>Idempotency-Key</c> header.
/// </summary>
/// <remarks>
/// <para>On first submission the response body and status are persisted via
/// <see cref="IIdempotencyStore"/> with a caller-configured TTL (default 24 h).
/// Re-submission of the same key serves the stored response without invoking
/// the downstream pipeline.</para>
/// <para>The request body is hashed and compared on cache hits. If a client
/// reuses a key with a different payload they get HTTP 409 — this prevents
/// accidental cross-talk between unrelated requests that collided on a key.</para>
/// </remarks>
public sealed class IdempotencyMiddleware(
    RequestDelegate next,
    IOptions<IdempotencyOptions> options,
    ILogger<IdempotencyMiddleware> logger)
{
    private const string HeaderName = "Idempotency-Key";

    private readonly RequestDelegate _next = next;
    private readonly IdempotencyOptions _options = options.Value;
    private readonly ILogger<IdempotencyMiddleware> _logger = logger;

    /// <summary>Middleware entry point.</summary>
    public async Task InvokeAsync(HttpContext context, IIdempotencyStore store)
    {
        if (!IsUnsafeMethod(context.Request.Method) ||
            !context.Request.Headers.TryGetValue(HeaderName, out var keyValues) ||
            string.IsNullOrWhiteSpace(keyValues.ToString()))
        {
            await _next(context);
            return;
        }

        var key = keyValues.ToString();
        var requestHash = await HashRequestBodyAsync(context.Request);

        var cached = await store.TryGetAsync(key, context.RequestAborted);
        if (cached is not null)
        {
            if (!string.Equals(cached.RequestHash, requestHash, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Idempotency-Key {Key} reused with a different request body; rejecting",
                    key);
                await WriteConflictAsync(context);
                return;
            }

            _logger.LogInformation("Idempotency-Key {Key} served from store", key);
            await WriteCachedAsync(context, cached);
            return;
        }

        // Capture the downstream response so it can be persisted.
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

        var bodyBytes = buffer.ToArray();

        if (ShouldCache(context.Response.StatusCode))
        {
            var snapshot = new CachedResponse(
                context.Response.StatusCode,
                context.Response.ContentType,
                bodyBytes,
                requestHash);

            await store.SaveAsync(key, snapshot, _options.RetentionHoursTimeSpan, context.RequestAborted);
        }

        await context.Response.Body.WriteAsync(bodyBytes, context.RequestAborted);
    }

    private static bool IsUnsafeMethod(string method) =>
        HttpMethods.IsPost(method) ||
        HttpMethods.IsPut(method) ||
        HttpMethods.IsPatch(method) ||
        HttpMethods.IsDelete(method);

    private static bool ShouldCache(int statusCode) =>
        statusCode is >= 200 and < 300;

    private static async Task<string> HashRequestBodyAsync(HttpRequest request)
    {
        request.EnableBuffering();
        request.Body.Position = 0;

        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(request.Body);

        // Rewind for downstream model binding.
        request.Body.Position = 0;

        return Convert.ToHexString(hash);
    }

    private static async Task WriteCachedAsync(HttpContext context, CachedResponse cached)
    {
        context.Response.StatusCode = cached.StatusCode;
        if (!string.IsNullOrEmpty(cached.ContentType))
        {
            context.Response.ContentType = cached.ContentType;
        }
        await context.Response.Body.WriteAsync(cached.Body);
    }

    private static async Task WriteConflictAsync(HttpContext context)
    {
        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync("""
            {"status":409,"title":"Idempotency key reused with a different request body"}
            """);
    }
}
