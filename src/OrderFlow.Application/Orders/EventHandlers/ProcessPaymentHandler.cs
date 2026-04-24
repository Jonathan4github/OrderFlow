using MediatR;
using Microsoft.Extensions.Logging;
using OrderFlow.Application.Abstractions.Payments;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Common.Resilience;
using OrderFlow.Domain.Enums;
using OrderFlow.Domain.Events;
using Polly.Registry;

namespace OrderFlow.Application.Orders.EventHandlers;

/// <summary>
/// First stage of the event-driven pipeline. Handles
/// <see cref="OrderPlacedDomainEvent"/> by calling the payment gateway,
/// recording the outcome on the aggregate, and raising the next event
/// (<see cref="PaymentProcessedDomainEvent"/>).
/// </summary>
public sealed class ProcessPaymentHandler(
    IPaymentGateway paymentGateway,
    IOrderRepository orders,
    IUnitOfWork unitOfWork,
    ResiliencePipelineProvider<string> pipelines,
    ILogger<ProcessPaymentHandler> logger) : INotificationHandler<OrderPlacedDomainEvent>
{
    private readonly IPaymentGateway _paymentGateway = paymentGateway;
    private readonly IOrderRepository _orders = orders;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ResiliencePipelineProvider<string> _pipelines = pipelines;
    private readonly ILogger<ProcessPaymentHandler> _logger = logger;

    /// <inheritdoc />
    public Task Handle(OrderPlacedDomainEvent notification, CancellationToken cancellationToken) =>
        _pipelines
            .GetPipeline(ResiliencePipelines.EventHandler)
            .ExecuteAsync(async ct => await ExecuteAsync(notification, ct), cancellationToken)
            .AsTask();

    private async Task ExecuteAsync(OrderPlacedDomainEvent notification, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(notification.OrderId, ct);
        if (order is null)
        {
            _logger.LogWarning(
                "ProcessPaymentHandler: order {OrderId} not found; skipping", notification.OrderId);
            return;
        }

        if (order.Status != OrderStatus.Pending)
        {
            _logger.LogInformation(
                "ProcessPaymentHandler: order {OrderId} already in status {Status}; skipping payment",
                order.Id, order.Status);
            return;
        }

        var result = await _paymentGateway.ChargeAsync(order.Id, order.CustomerId, order.TotalAmount, ct);

        if (result.IsSuccessful)
        {
            order.MarkPaymentSucceeded();
        }
        else
        {
            order.MarkPaymentFailed(result.FailureReason ?? "Unknown payment error");
        }

        _orders.Update(order);
        await _unitOfWork.SaveChangesAsync(ct);
    }
}
