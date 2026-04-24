using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OrderFlow.Domain.Common;
using OrderFlow.Infrastructure.Outbox;

namespace OrderFlow.Infrastructure.Persistence.Interceptors;

/// <summary>
/// EF Core interceptor that, immediately before <c>SaveChanges</c>, drains the
/// <see cref="AggregateRoot.DomainEvents"/> buckets of every tracked aggregate
/// into <see cref="OutboxMessage"/> rows. Because this runs inside the same
/// transaction as the business change, domain events are never published for
/// a failed transaction and never lost on success (outbox pattern).
/// </summary>
public sealed class OutboxMessageInterceptor : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            EnqueueDomainEvents(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            EnqueueDomainEvents(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void EnqueueDomainEvents(DbContext context)
    {
        var aggregates = context.ChangeTracker
            .Entries<AggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToArray();

        if (aggregates.Length == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                var eventType = domainEvent.GetType();
                var message = new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    Type = eventType.AssemblyQualifiedName
                        ?? throw new InvalidOperationException(
                            $"Domain event type {eventType.FullName} has no assembly-qualified name."),
                    Payload = JsonSerializer.Serialize(domainEvent, eventType, SerializerOptions),
                    CreatedAt = now,
                    ProcessedAt = null,
                    AttemptCount = 0
                };

                context.Set<OutboxMessage>().Add(message);
            }

            aggregate.ClearDomainEvents();
        }
    }
}