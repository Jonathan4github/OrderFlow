using Microsoft.Extensions.Logging;
using OrderFlow.Application.Abstractions.Payments;
using OrderFlow.Domain.ValueObjects;

namespace OrderFlow.Infrastructure.Services;

/// <summary>
/// Stand-in for a real payment provider. Always succeeds and emits a log
/// line so the end-to-end pipeline is observable. Replaced by a real
/// implementation (or a test double) as needed.
/// </summary>
public sealed class LoggingPaymentGateway(ILogger<LoggingPaymentGateway> logger) : IPaymentGateway
{
    private readonly ILogger<LoggingPaymentGateway> _logger = logger;

    /// <inheritdoc />
    public Task<PaymentResult> ChargeAsync(
        Guid orderId,
        Guid customerId,
        Money amount,
        CancellationToken cancellationToken = default)
    {
        var transactionId = $"sim_{Guid.NewGuid():N}";

        _logger.LogInformation(
            "[SIMULATED PAYMENT] Charged {Amount} {Currency} to customer {CustomerId} for order {OrderId} (txn {TransactionId})",
            amount.Amount, amount.Currency, customerId, orderId, transactionId);

        return Task.FromResult(PaymentResult.Success(transactionId));
    }
}