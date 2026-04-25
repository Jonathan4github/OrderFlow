using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrderFlow.Infrastructure.Persistence;

namespace OrderFlow.Infrastructure.Outbox;

/// <summary>
/// Reports the API as unhealthy when the oldest unprocessed
/// <see cref="OutboxMessage"/> is older than five minutes. A backed-up outbox
/// indicates the publisher is wedged or downstream handlers are failing
/// faster than they can succeed — both situations a load balancer should
/// react to before customer-visible side effects (emails, fulfilment) drift.
/// </summary>
public sealed class OutboxHealthCheck(AppDbContext db) : IHealthCheck
{
    /// <summary>Threshold above which the queue is considered stale.</summary>
    public static readonly TimeSpan StalenessThreshold = TimeSpan.FromMinutes(5);

    private readonly AppDbContext _db = db;

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var oldest = await _db.OutboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.CreatedAt)
            .Select(m => (DateTimeOffset?)m.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (oldest is null)
        {
            return HealthCheckResult.Healthy("outbox empty");
        }

        var age = DateTimeOffset.UtcNow - oldest.Value;
        var data = new Dictionary<string, object>
        {
            ["oldestPendingAgeSeconds"] = (int)age.TotalSeconds,
            ["thresholdSeconds"] = (int)StalenessThreshold.TotalSeconds
        };

        return age <= StalenessThreshold
            ? HealthCheckResult.Healthy($"oldest pending message is {age.TotalSeconds:F0}s old", data)
            : HealthCheckResult.Degraded($"outbox stale: oldest pending message is {age.TotalSeconds:F0}s old", data: data);
    }
}
