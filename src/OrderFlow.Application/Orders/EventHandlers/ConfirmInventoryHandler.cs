using MediatR;
using Microsoft.Extensions.Logging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Common.Resilience;
using OrderFlow.Domain.Enums;
using OrderFlow.Domain.Events;
using Polly.Registry;

namespace OrderFlow.Application.Orders.EventHandlers;

/// <summary>
/// Second stage of the event-driven pipeline. Handles
/// <see cref="PaymentProcessedDomainEvent"/>. On success, confirms each
/// reservation (stock moves from reserved → fulfilled) and transitions
/// the order to <see cref="OrderStatus.Confirmed"/>, raising
/// <see cref="InventoryConfirmedDomainEvent"/>. On failure, releases
/// the held reservations so the units return to the available pool.
/// </summary>
public sealed class ConfirmInventoryHandler(
    IOrderRepository orders,
    IInventoryRepository inventories,
    IUnitOfWork unitOfWork,
    ResiliencePipelineProvider<string> pipelines,
    ILogger<ConfirmInventoryHandler> logger) : INotificationHandler<PaymentProcessedDomainEvent>
{
    private readonly IOrderRepository _orders = orders;
    private readonly IInventoryRepository _inventories = inventories;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ResiliencePipelineProvider<string> _pipelines = pipelines;
    private readonly ILogger<ConfirmInventoryHandler> _logger = logger;

    /// <inheritdoc />
    public Task Handle(PaymentProcessedDomainEvent notification, CancellationToken cancellationToken) =>
        _pipelines
            .GetPipeline(ResiliencePipelines.EventHandler)
            .ExecuteAsync(async ct => await ExecuteAsync(notification, ct), cancellationToken)
            .AsTask();

    private async Task ExecuteAsync(PaymentProcessedDomainEvent notification, CancellationToken ct)
    {
        var order = await _orders.GetWithItemsAsync(notification.OrderId, ct);
        if (order is null)
        {
            _logger.LogWarning(
                "ConfirmInventoryHandler: order {OrderId} not found; skipping", notification.OrderId);
            return;
        }

        if (notification.IsSuccessful)
        {
            if (order.Status != OrderStatus.Pending)
            {
                _logger.LogInformation(
                    "ConfirmInventoryHandler: order {OrderId} already in {Status}; skipping confirmation",
                    order.Id, order.Status);
                return;
            }

            foreach (var line in order.Items)
            {
                var inventory = await _inventories.GetByIdAsync(line.ProductId, ct);
                if (inventory is null)
                {
                    _logger.LogError(
                        "ConfirmInventoryHandler: inventory for product {ProductId} missing during confirm; order {OrderId}",
                        line.ProductId, order.Id);
                    continue;
                }
                inventory.ConfirmReservation(line.Quantity);
                _inventories.Update(inventory);
            }

            order.Confirm();
            _orders.Update(order);
        }
        else
        {
            _logger.LogInformation(
                "ConfirmInventoryHandler: payment failed for order {OrderId}; releasing reservations",
                order.Id);

            foreach (var line in order.Items)
            {
                var inventory = await _inventories.GetByIdAsync(line.ProductId, ct);
                if (inventory is null)
                {
                    continue;
                }
                inventory.ReleaseReservation(line.Quantity);
                _inventories.Update(inventory);
            }
            // Order is already in Failed state (set by MarkPaymentFailed in ProcessPaymentHandler).
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }
}
