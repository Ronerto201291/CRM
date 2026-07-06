using Erp.Application.Common.Events;
using Erp.Modules.Billing.Application.Features.Billing.Commands;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Erp.Modules.Billing.Application.Handlers;

/// <summary>
/// Reacciona a ClientServiceDueForBillingEvent (publicado por
/// ContractedServiceBillingJob en Crm) generando la factura recurrente vía el
/// CreateInvoiceCommand real — sin duplicar lógica de facturación (numeración,
/// hash chain, IVA). Si tiene éxito, publica RecurringServiceInvoiceGeneratedEvent
/// para que Crm avance NextBillingDate del contrato.
/// </summary>
public class GenerateRecurringServiceInvoiceHandler : INotificationHandler<ClientServiceDueForBillingEvent>
{
    private readonly IMediator _mediator;
    private readonly IPublisher _publisher;
    private readonly ILogger<GenerateRecurringServiceInvoiceHandler> _logger;

    public GenerateRecurringServiceInvoiceHandler(IMediator mediator, IPublisher publisher, ILogger<GenerateRecurringServiceInvoiceHandler> logger)
    {
        _mediator = mediator;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task Handle(ClientServiceDueForBillingEvent notification, CancellationToken ct)
    {
        var invoice = await _mediator.Send(new CreateInvoiceCommand
        {
            ClientId = notification.ClientId,
            ClientType = "Registered",
            Series = "A",
            DueDate = DateTime.UtcNow.Date.AddDays(30),
            IrpfRate = 0,
            Lines =
            [
                new CreateInvoiceLineDto
                {
                    Description = notification.ServiceName,
                    Quantity = 1,
                    UnitPrice = notification.Price,
                    TaxRate = notification.TaxRate,
                    SurchargeRate = 0,
                    TipoOperacion = "Nacional",
                }
            ],
        }, ct);

        _logger.LogInformation(
            "GenerateRecurringServiceInvoiceHandler: created invoice {InvoiceNumber} for contracted service {ContractId} (Company: {CompanyId})",
            invoice.Number, notification.ClientContractedServiceId, notification.CompanyId);

        await _publisher.Publish(new RecurringServiceInvoiceGeneratedEvent
        {
            ClientContractedServiceId = notification.ClientContractedServiceId,
            InvoiceId = invoice.Id,
            CompanyId = notification.CompanyId,
        }, ct);
    }
}
