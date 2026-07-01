using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;

namespace Erp.Infrastructure.Interceptors;

public class AuditInterceptor
{
    public static void ProcessAuditEntries(IApplicationDbContext context, Guid? userId, Guid? companyId)
    {
        if (context is not DbContext dbContext) return;

        var entries = dbContext.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId ?? Guid.Empty,
                UserId = userId ?? Guid.Empty,
                Entity = entry.Entity.GetType().Name,
                Action = entry.State.ToString(),
                Timestamp = DateTime.UtcNow,
                OldValues = entry.State == EntityState.Added
                    ? "{}"
                    : JsonSerializer.Serialize(entry.OriginalValues.Properties.ToDictionary(p => p.Name, p => entry.OriginalValues[p]?.ToString())),
                NewValues = entry.State == EntityState.Deleted
                    ? "{}"
                    : JsonSerializer.Serialize(entry.CurrentValues.Properties.ToDictionary(p => p.Name, p => entry.CurrentValues[p]?.ToString()))
            };
            dbContext.Set<AuditLog>().Add(auditLog);
        }
    }
}
