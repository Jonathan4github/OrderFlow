using OrderFlow.Domain.ValueObjects;

namespace OrderFlow.Application.Abstractions.Payments;

/// <summary>Port into an external payment provider.</summary>
public interface IPaymentGateway
{
    /// <summary>Charges <paramref name="amount"/> to the customer for the given order.</summary>
    Task<PaymentResult> ChargeAsync(
        Guid orderId,
        Guid customerId,
        Money amount,
        CancellationToken cancellationToken = default);
}

/// <summary>Outcome of a payment attempt.</summary>
/// <param name="IsSuccessful">True if the charge was accepted.</param>
/// <param name="TransactionId">Identifier returned by the provider (success only).</param>
/// <param name="FailureReason">Short reason string (failure only).</param>
public sealed record PaymentResult(
    bool IsSuccessful,
    string? TransactionId,
    string? FailureReason)
{
    /// <summary>Convenience factory for a successful payment.</summary>
    public static PaymentResult Success(string transactionId) => new(true, transactionId, null);

    /// <summary>Convenience factory for a failed payment.</summary>
    public static PaymentResult Failure(string reason) => new(false, null, reason);
}