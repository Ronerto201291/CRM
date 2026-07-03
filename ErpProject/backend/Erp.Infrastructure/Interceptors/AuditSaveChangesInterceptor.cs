using Erp.Application.Common.Interfaces;
using Erp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Infrastructure.Interceptors;

/// <summary>Persiste audit trail en ErpDbContext tras cambios en cualquier DbContext (ADR-0018 #31).</summary>
public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextCurrentUserAccessor _userAccessor;
    private readonly ITenantContext _tenantContext;
    private readonly IServiceProvider _serviceProvider;

    public AuditSaveChangesInterceptor(
        IHttpContextCurrentUserAccessor userAccessor,
        ITenantContext tenantContext,
        IServiceProvider serviceProvider)
    {
        _userAccessor = userAccessor;
        _tenantContext = tenantContext;
        _serviceProvider = serviceProvider;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is ErpDbContext erpDb)
        {
            AuditInterceptor.ProcessAuditEntries(
                erpDb,
                _userAccessor.UserId,
                _tenantContext.TenantId);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (result > 0
            && eventData.Context is not null
            && eventData.Context is not ErpDbContext)
        {
            var logs = AuditInterceptor.CollectAuditEntries(
                eventData.Context,
                _userAccessor.UserId,
                _tenantContext.TenantId);

            if (logs.Count > 0)
            {
                var auditDb = _serviceProvider.GetRequiredService<ErpDbContext>();
                foreach (var log in logs)
                    auditDb.AuditLogs.Add(log);
                await auditDb.SaveChangesAsync(cancellationToken);
            }
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }
}
