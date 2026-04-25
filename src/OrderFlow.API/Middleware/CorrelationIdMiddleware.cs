using Serilog.Context;

namespace OrderFlow.API.Middleware;

/// <summary>
/// Reads the inbound <c>X-Correlation-ID</c> header (or mints a new GUID when
/// absent), pushes it into Serilog's <see cref="LogContext"/> so every log line
/// emitted during the request carries it, and echoes it back on the response so
/// callers can correlate their request to server-side logs.
/// </summary>
/// <remarks>
/// Must run before <see cref="GlobalExceptionHandlerMiddleware"/> so the
/// correlation id is also attached to error logs and ProblemDetails responses.
/// </remarks>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    /// <summary>HTTP header used for the correlation id.</summary>
    public const string HeaderName = "X-Correlation-ID";

    /// <summary>HttpContext.Items key used to expose the id to downstream code.</summary>
    public const string ItemsKey = "CorrelationId";

    private readonly RequestDelegate _next = next;

    /// <summary>Middleware entry point.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);

        context.Items[ItemsKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty(ItemsKey, correlationId))
        {
            await _next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var values) &&
            !string.IsNullOrWhiteSpace(values.ToString()))
        {
            return values.ToString();
        }

        return Guid.NewGuid().ToString("N");
    }
}
