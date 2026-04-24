namespace OrderFlow.Application.Abstractions.Notifications;

/// <summary>Port into an outbound email transport.</summary>
public interface IEmailNotifier
{
    /// <summary>
    /// Sends the "order confirmed" email to the customer. Implementations are
    /// expected to be best-effort — retries are orchestrated by the caller.
    /// </summary>
    Task SendOrderConfirmedAsync(
        Guid customerId,
        Guid orderId,
        CancellationToken cancellationToken = default);
}