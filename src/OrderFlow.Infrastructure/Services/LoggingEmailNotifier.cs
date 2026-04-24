using Microsoft.Extensions.Logging;
using OrderFlow.Application.Abstractions.Notifications;

namespace OrderFlow.Infrastructure.Services;

/// <summary>
/// Stand-in for a real email transport. Logs the dispatch so reviewers can
/// see the pipeline reach its terminal step without requiring an SMTP setup.
/// </summary>
public sealed class LoggingEmailNotifier(ILogger<LoggingEmailNotifier> logger) : IEmailNotifier
{
    private readonly ILogger<LoggingEmailNotifier> _logger = logger;

    /// <inheritdoc />
    public Task SendOrderConfirmedAsync(
        Guid customerId,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[SIMULATED EMAIL] Order {OrderId} confirmation sent to customer {CustomerId}",
            orderId, customerId);
        return Task.CompletedTask;
    }
}