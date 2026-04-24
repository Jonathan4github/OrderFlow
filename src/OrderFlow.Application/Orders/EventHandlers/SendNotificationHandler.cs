using MediatR;
using Microsoft.Extensions.Logging;
using OrderFlow.Application.Abstractions.Notifications;
using OrderFlow.Application.Common.Resilience;
using OrderFlow.Domain.Events;
using Polly.Registry;

namespace OrderFlow.Application.Orders.EventHandlers;

/// <summary>
/// Terminal stage of the event-driven pipeline. Handles
/// <see cref="InventoryConfirmedDomainEvent"/> by dispatching an
/// "order confirmed" notification to the customer. Retried with
/// exponential back-off like the other handlers so transient SMTP /
/// notification-provider glitches do not lose customer communication.
/// </summary>
public sealed class SendNotificationHandler(
    IEmailNotifier notifier,
    ResiliencePipelineProvider<string> pipelines,
    ILogger<SendNotificationHandler> logger) : INotificationHandler<InventoryConfirmedDomainEvent>
{
    private readonly IEmailNotifier _notifier = notifier;
    private readonly ResiliencePipelineProvider<string> _pipelines = pipelines;
    private readonly ILogger<SendNotificationHandler> _logger = logger;

    /// <inheritdoc />
    public Task Handle(InventoryConfirmedDomainEvent notification, CancellationToken cancellationToken) =>
        _pipelines
            .GetPipeline(ResiliencePipelines.EventHandler)
            .ExecuteAsync(async ct => await ExecuteAsync(notification, ct), cancellationToken)
            .AsTask();

    private async Task ExecuteAsync(InventoryConfirmedDomainEvent notification, CancellationToken ct)
    {
        _logger.LogInformation(
            "SendNotificationHandler: dispatching confirmation for order {OrderId}", notification.OrderId);

        await _notifier.SendOrderConfirmedAsync(notification.CustomerId, notification.OrderId, ct);
    }
}
