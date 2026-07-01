using Erp.Application.Common.Events;
using Erp.Modules.Crm.Application.Interfaces;
using Erp.Modules.Crm.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Erp.Modules.Crm.Application.EventHandlers;

/// <summary>
/// Cuando un presupuesto con ClientType=Lead es aceptado (portal o interno),
/// convierte automáticamente el Lead en un Client registrado.
/// </summary>
public class QuoteAcceptedCrmHandler : INotificationHandler<QuoteAcceptedEvent>
{
    private readonly ICrmDbContext _crmCtx;
    private readonly ILogger<QuoteAcceptedCrmHandler> _logger;

    public QuoteAcceptedCrmHandler(ICrmDbContext crmCtx, ILogger<QuoteAcceptedCrmHandler> logger)
    {
        _crmCtx = crmCtx;
        _logger = logger;
    }

    public async Task Handle(QuoteAcceptedEvent notification, CancellationToken ct)
    {
        // Solo actuar si el presupuesto era de un posible cliente (Lead)
        if (notification.ClientType != "Lead" || !notification.ClientId.HasValue)
            return;

        var leadId = notification.ClientId.Value;

        // IgnoreQueryFilters para buscar por Id sin filtro de tenant (el CompanyId lo verificamos manualmente)
        var lead = await _crmCtx.Leads
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.Id == leadId && l.CompanyId == notification.CompanyId, ct);

        if (lead is null)
        {
            _logger.LogWarning("QuoteAccepted: Lead {LeadId} no encontrado para empresa {CompanyId}", leadId, notification.CompanyId);
            return;
        }

        // Si ya fue convertido, no duplicar
        if (lead.ConvertedToClientId.HasValue)
        {
            _logger.LogInformation("QuoteAccepted: Lead {LeadId} ya convertido en cliente {ClientId}", leadId, lead.ConvertedToClientId);
            return;
        }

        var client = new Client
        {
            Id           = Guid.NewGuid(),
            CompanyId    = notification.CompanyId,
            Name         = lead.Name,
            TaxId        = lead.TaxId,
            Email        = lead.Email,
            Phone        = lead.Phone,
            Address      = lead.Address,
            CustomFields = "{}",
        };

        _crmCtx.Clients.Add(client);

        lead.Status              = "Won";
        lead.ConvertedToClientId = client.Id;

        _crmCtx.ActivityLogs.Add(new ActivityLog
        {
            Id          = Guid.NewGuid(),
            CompanyId   = notification.CompanyId,
            EntityType  = "Lead", EntityId = lead.Id,
            Action      = "ConvertedToClient",
            Description = $"Lead '{lead.Name}' convertido automáticamente al aceptar el presupuesto {notification.QuoteNumber}"
        });

        _crmCtx.ActivityLogs.Add(new ActivityLog
        {
            Id          = Guid.NewGuid(),
            CompanyId   = notification.CompanyId,
            EntityType  = "Client", EntityId = client.Id,
            Action      = "CreatedFromLead",
            Description = $"Cliente creado automáticamente desde posible cliente al aceptar presupuesto {notification.QuoteNumber}"
        });

        await _crmCtx.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Lead {LeadId} ({Name}) convertido en Client {ClientId} al aceptar presupuesto {QuoteNumber}",
            leadId, lead.Name, client.Id, notification.QuoteNumber);
    }
}
