using Erp.Application.Common.Events;
using Erp.Modules.Billing.Application.Features.Billing.Commands;
using Erp.Modules.Billing.Application.Features.Billing.Handlers;
using Erp.Tests.TestSupport;
using Xunit;

namespace Erp.Tests.Billing;

public class MarkInvoicePaidFromStripeHandlerTests
{
    [Fact]
    public async Task Handle_ForwardsToMarkPaidCommandWithCardPaymentMethod()
    {
        var invoiceId = Guid.NewGuid();
        var mediator = new FakeMediator(_ => true);
        var handler = new MarkInvoicePaidFromStripeHandler(mediator);

        await handler.Handle(new StripeInvoiceCheckoutCompletedEvent { InvoiceId = invoiceId }, CancellationToken.None);

        var sent = Assert.IsType<MarkPaidCommand>(Assert.Single(mediator.SentRequests));
        Assert.Equal(invoiceId, sent.Id);
        Assert.Equal("card", sent.PaymentMethod);
    }
}
