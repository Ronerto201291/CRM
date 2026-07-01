using Erp.Application.Common.Events;
using Erp.Modules.Crm.Application.Interfaces;
using Erp.Modules.Crm.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Crm.Application.EventHandlers;

/// <summary>
/// When an expense is approved, creates CRM ActivityLog entries:
/// - One linked to the ExpenseDocument (always)
/// - One linked to the Supplier (when SupplierId is set)
/// This keeps inter-module communication event-driven with no direct reference.
/// </summary>
public class ExpenseApprovedActivityHandler : INotificationHandler<ExpenseApprovedEvent>
{
    private readonly ICrmDbContext _crm;
    public ExpenseApprovedActivityHandler(ICrmDbContext crm) => _crm = crm;

    public async Task Handle(ExpenseApprovedEvent notification, CancellationToken ct)
    {
        // Idempotency: skip if activity log already created for this expense approval
        var exists = await _crm.ActivityLogs
            .AnyAsync(a => a.EntityType == "ExpenseDocument"
                        && a.EntityId == notification.ExpenseDocumentId
                        && a.Action == "Approved", ct);
        if (exists) return;

        var now = DateTime.UtcNow;

        _crm.ActivityLogs.Add(new ActivityLog
        {
            Id = Guid.NewGuid(), CompanyId = notification.CompanyId,
            EntityType = "ExpenseDocument", EntityId = notification.ExpenseDocumentId,
            Action = "Approved",
            Description = $"Gasto aprobado: {notification.SupplierName} — {notification.Total:C}",
            Timestamp = now
        });

        if (notification.SupplierId.HasValue)
        {
            _crm.ActivityLogs.Add(new ActivityLog
            {
                Id = Guid.NewGuid(), CompanyId = notification.CompanyId,
                EntityType = "Supplier", EntityId = notification.SupplierId.Value,
                Action = "ExpenseApproved",
                Description = $"Factura {notification.SupplierName} aprobada — {notification.Total:C}",
                Timestamp = now
            });
        }

        await _crm.SaveChangesAsync(ct);
    }
}
