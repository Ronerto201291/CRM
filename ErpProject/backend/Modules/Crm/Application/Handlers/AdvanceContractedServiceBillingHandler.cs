using Erp.Application.Common.Events;
using Erp.Modules.Crm.Application.Features.Services;
using Erp.Modules.Crm.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Erp.Modules.Crm.Application.Handlers;

/// <summary>
/// Reacciona a RecurringServiceInvoiceGeneratedEvent (publicado por
/// GenerateRecurringServiceInvoiceHandler en Billing) avanzando NextBillingDate
/// y LastInvoiceId del contrato — cierra el ciclo abierto por
/// ContractedServiceBillingJob (ver ADR-0018 #42f).
/// </summary>
public class AdvanceContractedServiceBillingHandler : INotificationHandler<RecurringServiceInvoiceGeneratedEvent>
{
    private readonly ICrmDbContext _context;
    private readonly ILogger<AdvanceContractedServiceBillingHandler> _logger;

    public AdvanceContractedServiceBillingHandler(ICrmDbContext context, ILogger<AdvanceContractedServiceBillingHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Handle(RecurringServiceInvoiceGeneratedEvent notification, CancellationToken ct)
    {
        var contract = await _context.ClientContractedServices
            .FirstOrDefaultAsync(c => c.Id == notification.ClientContractedServiceId, ct);

        if (contract == null)
        {
            _logger.LogWarning(
                "AdvanceContractedServiceBillingHandler: contracted service {ContractId} not found (Company: {CompanyId}).",
                notification.ClientContractedServiceId, notification.CompanyId);
            return;
        }

        contract.LastInvoiceId = notification.InvoiceId;
        contract.NextBillingDate = ServicePeriodicity.AddPeriod(contract.NextBillingDate, contract.Periodicity);
        contract.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
    }
}
