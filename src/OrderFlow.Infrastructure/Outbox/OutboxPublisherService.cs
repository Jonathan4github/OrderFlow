using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrderFlow.Infrastructure.Persistence;

namespace OrderFlow.Infrastructure.Outbox;

/// <summary>
/// Background worker that drains <see cref="OutboxMessage"/> rows by rehydrating
/// each payload back into its original CLR type and publishing it through
/// MediatR. Successful dispatches mark the row as processed; failures increment
/// the attempt counter and leave the row for a future retry until
/// <see cref="OutboxOptions.MaxAttempts"/> is reached.
/// </summary>
public sealed class OutboxPublisherService(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    ILogger<OutboxPublisherService> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly OutboxOptions _options = options.Value;
    private readonly ILogger<OutboxPublisherService> _logger = logger;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(Math.Max(1, _options.PollingIntervalSeconds));
        _logger.LogInformation(
            "OutboxPublisher started. Poll = {Delay}s, batch = {Batch}, maxAttempts = {Max}",
            delay.TotalSeconds, _options.BatchSize, _options.MaxAttempts);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OutboxPublisher tick failed");
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        var pending = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.AttemptCount < _options.MaxAttempts)
            .OrderBy(m => m.CreatedAt)
            .Take(Math.Max(1, _options.BatchSize))
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return;
        }

        foreach (var message in pending)
        {
            await PublishAsync(publisher, message, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task PublishAsync(
        IPublisher publisher, OutboxMessage message, CancellationToken cancellationToken)
    {
        try
        {
            var eventType = Type.GetType(message.Type, throwOnError: false);
            if (eventType is null)
            {
                throw new InvalidOperationException(
                    $"Could not resolve domain event CLR type '{message.Type}'.");
            }

            var payload = JsonSerializer.Deserialize(message.Payload, eventType, SerializerOptions)
                ?? throw new InvalidOperationException(
                    $"Deserialised payload for outbox message {message.Id} was null.");

            if (payload is not INotification notification)
            {
                throw new InvalidOperationException(
                    $"Event type {eventType.FullName} is not a MediatR INotification.");
            }

            await publisher.Publish(notification, cancellationToken);

            message.ProcessedAt = DateTimeOffset.UtcNow;
            message.Error = null;
            message.AttemptCount++;

            _logger.LogDebug(
                "Outbox message {MessageId} ({Type}) dispatched on attempt {Attempt}",
                message.Id, eventType.Name, message.AttemptCount);
        }
        catch (Exception ex)
        {
            message.AttemptCount++;
            message.Error = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;

            _logger.LogError(
                ex,
                "Outbox message {MessageId} ({Type}) failed on attempt {Attempt}",
                message.Id, message.Type, message.AttemptCount);
        }
    }
}
