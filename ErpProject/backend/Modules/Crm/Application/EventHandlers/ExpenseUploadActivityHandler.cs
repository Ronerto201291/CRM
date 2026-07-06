using Erp.Application.Common.Events;
using Erp.Modules.Crm.Application.Interfaces;
using Erp.Modules.Crm.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Crm.Application.EventHandlers;

/// <summary>
/// Registra en ActivityLog la subida de un gasto vía QR (sin acoplar Expenses a ICrmDbContext).
/// </summary>
public class ExpenseUploadActivityHandler : INotificationHandler<ExpenseUploadCreatedEvent>
{
    private readonly ICrmDbContext _crm;

    public ExpenseUploadActivityHandler(ICrmDbContext crm) => _crm = crm;

    public async Task Handle(ExpenseUploadCreatedEvent notification, CancellationToken ct)
    {
        var exists = await _crm.ActivityLogs
            .AnyAsync(a => a.EntityType == "ExpenseUpload"
                        && a.EntityId == notification.UploadId
                        && a.Action == "Uploaded", ct);
        if (exists) return;

        _crm.ActivityLogs.Add(new ActivityLog
        {
            Id = Guid.NewGuid(),
            CompanyId = notification.CompanyId,
            EntityType = "ExpenseUpload",
            EntityId = notification.UploadId,
            Action = "Uploaded",
            Description = $"Documento subido via QR: {notification.FileName}",
            Timestamp = DateTime.UtcNow
        });

        await _crm.SaveChangesAsync(ct);
    }
}
