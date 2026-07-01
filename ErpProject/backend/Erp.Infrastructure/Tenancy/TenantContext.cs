using Erp.Application.Common.Interfaces;

namespace Erp.Infrastructure.Tenancy;

public class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }
    public string? TenantName { get; private set; }

    public void SetTenant(Guid tenantId, string tenantName)
    {
        TenantId = tenantId;
        TenantName = tenantName;
    }
}
