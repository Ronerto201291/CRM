using Erp.Application.Common.Events;
using Erp.Modules.Crm.Application.Interfaces;
using Erp.Modules.Crm.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Crm.Application.EventHandlers;

/// <summary>
/// When an invoice is paid, records two CRM activity logs:
/// - One on the Invoice entity (Action="Paid")
/// - One on the Client entity (Action="InvoicePaid")
/// Idempotency: checks ActivityLog before inserting.
/// </summary>
public class PaymentReceivedActivityHandler : INotificationHandler<PaymentReceivedEvent>
{
    private readonly ICrmDbContext _crm;
    public PaymentReceivedActivityHandler(ICrmDbContext crm) => _crm = crm;

    public async Task Handle(PaymentReceivedEvent notification, CancellationToken ct)
    {
        // Idempotency: skip if activity already logged for this payment
        var exists = await _crm.ActivityLogs
            .AnyAsync(a => a.EntityType == "Invoice"
                        && a.EntityId == notification.InvoiceId
                        && a.Action == "Paid", ct);
        if (exists) return;

        var now = DateTime.UtcNow;

        _crm.ActivityLogs.Add(new ActivityLog
        {
            Id          = Guid.NewGuid(),
            CompanyId   = notification.CompanyId,
            EntityType  = "Invoice",
            EntityId    = notification.InvoiceId,
            Action      = "Paid",
            Description = $"Factura {notification.InvoiceNumber} cobrada — {notification.Amount:C} ({notification.PaymentMethod})",
            Timestamp   = now
        });

        _crm.ActivityLogs.Add(new ActivityLog
        {
            Id          = Guid.NewGuid(),
            CompanyId   = notification.CompanyId,
            EntityType  = "Client",
            EntityId    = notification.ClientId.GetValueOrDefault(),
            Action      = "InvoicePaid",
            Description = $"Cobro recibido: factura {notification.InvoiceNumber} — {notification.Amount:C}",
            Timestamp   = now
        });

        await _crm.SaveChangesAsync(ct);
    }
}
