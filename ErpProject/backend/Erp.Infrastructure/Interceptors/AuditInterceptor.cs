using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Audit;
using Erp.Domain.Entities.Outbox;
using Erp.Domain.Entities.Licensing;
using Erp.Domain.Entities.Api;
using Erp.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Erp.Infrastructure.Interceptors;

public static class AuditInterceptor
{
    private static readonly HashSet<string> ExcludedTypes =
    [
        nameof(AuditLog),
        nameof(OutboxMessage),
        nameof(ApiUsageLog),
        nameof(RefreshToken),
        nameof(StripeWebhookEvent),
    ];

    public static List<AuditLog> CollectAuditEntries(DbContext source, Guid? userId, Guid? companyId)
    {
        var logs = new List<AuditLog>();
        if (!userId.HasValue || !companyId.HasValue || companyId == Guid.Empty)
            return logs;

        var entries = source.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(e => !ExcludedTypes.Contains(e.Entity.GetType().Name))
            .ToList();

        foreach (var entry in entries)
        {
            var entityId = TryGetPrimaryKey(entry);
            var oldValues = entry.State == EntityState.Added
                ? "{}"
                : SerializeValues(entry.OriginalValues);
            var newValues = entry.State == EntityState.Deleted
                ? "{}"
                : SerializeValues(entry.CurrentValues);

            var log = new AuditLog
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId.Value,
                UserId = userId.Value,
                Entity = entry.Entity.GetType().Name,
                EntityId = entityId,
                Action = entry.State.ToString(),
                Timestamp = DateTime.UtcNow,
                OldValues = oldValues,
                NewValues = newValues
            };
            log.Hash = ComputeHash(log);
            logs.Add(log);
        }

        return logs;
    }

    public static void ProcessAuditEntries(IApplicationDbContext context, Guid? userId, Guid? companyId)
    {
        if (context is not DbContext dbContext) return;
        foreach (var log in CollectAuditEntries(dbContext, userId, companyId))
            dbContext.Set<AuditLog>().Add(log);
    }

    private static Guid? TryGetPrimaryKey(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key == null || key.Properties.Count != 1) return null;
        var value = entry.Property(key.Properties[0].Name).CurrentValue
                 ?? entry.Property(key.Properties[0].Name).OriginalValue;
        return value is Guid g ? g : null;
    }

    private static string SerializeValues(PropertyValues values) =>
        JsonSerializer.Serialize(values.Properties.ToDictionary(
            p => p.Name,
            p => values[p]?.ToString()));

    private static string ComputeHash(AuditLog log)
    {
        var payload = $"{log.Entity}|{log.EntityId}|{log.Action}|{log.Timestamp:O}|{log.OldValues}|{log.NewValues}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
