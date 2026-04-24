using MediatR;
using Microsoft.Extensions.Logging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Domain.Exceptions;
using OrderFlow.Domain.Orders;
using OrderFlow.Domain.Products;

namespace OrderFlow.Application.Orders.PlaceOrder;

/// <summary>
/// Handles <see cref="PlaceOrderCommand"/>. Loads the referenced products,
/// locks the matching inventory rows, reserves stock, creates the
/// <see cref="Order"/> aggregate, and commits everything in a single
/// database transaction. Domain events raised by the aggregate are queued
/// to the outbox by the infrastructure layer during <c>SaveChanges</c>.
/// </summary>
public sealed class PlaceOrderCommandHandler(
    IProductRepository products,
    IInventoryRepository inventories,
    IOrderRepository orders,
    IUnitOfWork unitOfWork,
    ILogger<PlaceOrderCommandHandler> logger) : IRequestHandler<PlaceOrderCommand, PlaceOrderResult>
{
    private readonly IProductRepository _products = products;
    private readonly IInventoryRepository _inventories = inventories;
    private readonly IOrderRepository _orders = orders;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<PlaceOrderCommandHandler> _logger = logger;

    /// <inheritdoc />
    public Task<PlaceOrderResult> Handle(PlaceOrderCommand request, CancellationToken cancellationToken) =>
        _unitOfWork.ExecuteInTransactionAsync(ct => HandleCore(request, ct), cancellationToken);

    private async Task<PlaceOrderResult> HandleCore(PlaceOrderCommand request, CancellationToken ct)
    {
        var requestedIds = request.Items.Select(i => i.ProductId).Distinct().ToArray();

        var products = await _products.GetManyAsync(requestedIds, ct);
        var productsById = products.ToDictionary(p => p.Id);

        var missing = requestedIds.FirstOrDefault(id => !productsById.ContainsKey(id));
        if (missing != Guid.Empty)
        {
            throw new ProductNotFoundException(missing);
        }

        var lines = new List<(Product Product, int Quantity)>(request.Items.Count);
        foreach (var item in request.Items)
        {
            var product = productsById[item.ProductId];
            var inventory = await _inventories.GetForUpdateAsync(product.Id, ct);
            if (inventory is null)
            {
                // Missing inventory row is a configuration error; treat it as zero stock.
                throw new InsufficientStockException(product.Id, item.Quantity, available: 0);
            }

            inventory.Reserve(item.Quantity);
            _inventories.Update(inventory);
            lines.Add((product, item.Quantity));
        }

        var order = Order.Place(request.CustomerId, lines);
        await _orders.AddAsync(order, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Order {OrderId} placed for customer {CustomerId} with {LineCount} line(s), total {Total} {Currency}",
            order.Id, order.CustomerId, order.Items.Count, order.TotalAmount.Amount, order.TotalAmount.Currency);

        return new PlaceOrderResult(
            order.Id,
            order.Status.ToString(),
            order.TotalAmount.Amount,
            order.TotalAmount.Currency,
            order.PlacedAt);
    }
}
