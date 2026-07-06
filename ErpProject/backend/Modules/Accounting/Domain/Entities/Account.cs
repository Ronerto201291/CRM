using Erp.Domain.Common;

namespace Erp.Modules.Accounting.Domain.Entities;

/// <summary>Plan contable (PGC). ADR-0018 #14: migrada desde Erp.Domain.</summary>
public class Account : BaseEntity
{
    public Guid CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}
