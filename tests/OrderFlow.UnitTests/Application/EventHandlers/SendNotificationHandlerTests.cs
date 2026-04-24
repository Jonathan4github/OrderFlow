using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrderFlow.Application.Abstractions.Notifications;
using OrderFlow.Application.Orders.EventHandlers;
using OrderFlow.Domain.Events;

namespace OrderFlow.UnitTests.Application.EventHandlers;

public class SendNotificationHandlerTests
{
    private readonly Mock<IEmailNotifier> _notifier = new();

    private SendNotificationHandler CreateSut() => new(
        _notifier.Object,
        new TestResiliencePipelineProvider(),
        NullLogger<SendNotificationHandler>.Instance);

    [Fact]
    public async Task Calls_email_notifier_for_the_order()
    {
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        await CreateSut().Handle(
            new InventoryConfirmedDomainEvent(orderId, customerId, []),
            CancellationToken.None);

        _notifier.Verify(
            n => n.SendOrderConfirmedAsync(customerId, orderId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}