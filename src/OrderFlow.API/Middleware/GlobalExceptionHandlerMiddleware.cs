using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using OrderFlow.Application.Common.Exceptions;
using OrderFlow.Domain.Exceptions;
using ApplicationValidationException = OrderFlow.Application.Common.Exceptions.ValidationException;

namespace OrderFlow.API.Middleware;

/// <summary>
/// Catches unhandled exceptions bubbling up from controllers and maps them to
/// RFC-7807 <see cref="ProblemDetails"/> responses with the correct status code.
/// Unknown exceptions surface as 500 with a generic message; full details stay in logs.
/// </summary>
public sealed class GlobalExceptionHandlerMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionHandlerMiddleware> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next = next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger = logger;

    /// <summary>Middleware entry point.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var (status, title, extras) = Map(exception);

        if (status >= 500)
        {
            _logger.LogError(exception, "Unhandled exception processing {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }
        else
        {
            _logger.LogWarning(
                "Handled {ExceptionType} processing {Method} {Path}: {Message}",
                exception.GetType().Name, context.Request.Method, context.Request.Path, exception.Message);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status >= 500 ? "An unexpected error occurred." : exception.Message,
            Instance = context.Request.Path,
            Type = $"https://httpstatuses.io/{status}"
        };
        foreach (var kv in extras)
        {
            problem.Extensions[kv.Key] = kv.Value;
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        await JsonSerializer.SerializeAsync(context.Response.Body, problem, SerializerOptions);
    }

    private static (int Status, string Title, IReadOnlyDictionary<string, object?> Extras) Map(Exception ex) =>
        ex switch
        {
            ApplicationValidationException v => (
                StatusCodes.Status400BadRequest,
                "Validation failed",
                new Dictionary<string, object?> { ["errors"] = v.Errors }),

            ProductNotFoundException p => (
                StatusCodes.Status422UnprocessableEntity,
                "Product not found",
                new Dictionary<string, object?> { ["productId"] = p.ProductId }),

            InsufficientStockException s => (
                StatusCodes.Status409Conflict,
                "Insufficient stock",
                new Dictionary<string, object?>
                {
                    ["productId"] = s.ProductId,
                    ["requested"] = s.Requested,
                    ["available"] = s.Available
                }),

            InvalidOrderStateException => (
                StatusCodes.Status409Conflict,
                "Invalid order state",
                new Dictionary<string, object?>()),

            ConcurrencyConflictException => (
                StatusCodes.Status409Conflict,
                "Concurrent modification",
                new Dictionary<string, object?> { ["retryable"] = true }),

            DomainException => (
                StatusCodes.Status400BadRequest,
                "Domain rule violated",
                new Dictionary<string, object?>()),

            OperationCanceledException => (
                499, // Client Closed Request (non-standard)
                "Request cancelled",
                new Dictionary<string, object?>()),

            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal server error",
                new Dictionary<string, object?>())
        };
}
