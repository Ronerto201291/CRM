using Erp.Application.Common.Events;
using Erp.Application.DTOs;
using Erp.Modules.Billing.Application.Features.Billing.Commands;
using Erp.Modules.Billing.Application.Handlers;
using Erp.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Erp.Tests.Billing;

public class GenerateRecurringServiceInvoiceHandlerTests
{
    [Fact]
    public async Task Handle_SendsCreateInvoiceCommandWithServiceLineAndPublishesFollowUpEvent()
    {
        var invoiceId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var contractId = Guid.NewGuid();

        var canned = new InvoiceDto { Id = invoiceId, Number = "A-2026-000042" };
        var mediator = new FakeMediator(_ => canned);
        var publisher = new FakePublisher();
        var handler = new GenerateRecurringServiceInvoiceHandler(mediator, publisher, NullLogger<GenerateRecurringServiceInvoiceHandler>.Instance);

        await handler.Handle(new ClientServiceDueForBillingEvent
        {
            ClientContractedServiceId = contractId,
            ClientId = clientId,
            CompanyId = companyId,
            ServiceName = "Mantenimiento anual",
            Price = 199.99m,
            TaxRate = 21m,
            PeriodStart = DateTime.UtcNow.Date,
        }, CancellationToken.None);

        var sent = Assert.IsType<CreateInvoiceCommand>(Assert.Single(mediator.SentRequests));
        Assert.Equal(clientId, sent.ClientId);
        Assert.Equal("Registered", sent.ClientType);
        var line = Assert.Single(sent.Lines);
        Assert.Equal("Mantenimiento anual", line.Description);
        Assert.Equal(1, line.Quantity);
        Assert.Equal(199.99m, line.UnitPrice);
        Assert.Equal(21m, line.TaxRate);

        var published = Assert.IsType<RecurringServiceInvoiceGeneratedEvent>(Assert.Single(publisher.Published));
        Assert.Equal(contractId, published.ClientContractedServiceId);
        Assert.Equal(invoiceId, published.InvoiceId);
        Assert.Equal(companyId, published.CompanyId);
    }
}
