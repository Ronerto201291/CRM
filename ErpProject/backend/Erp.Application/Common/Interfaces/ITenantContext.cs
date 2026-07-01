namespace Erp.Application.Common.Interfaces;

public interface ITenantContext
{
    Guid? TenantId { get; }
    string? TenantName { get; }
    void SetTenant(Guid tenantId, string tenantName);
}
