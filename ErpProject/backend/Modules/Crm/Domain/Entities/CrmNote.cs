using Erp.Domain.Common;

namespace Erp.Modules.Crm.Domain.Entities;

public class CrmNote : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public string EntityType { get; set; } = ""; // "Client" | "Lead"
    public Guid EntityId { get; set; }
    public string? Title { get; set; }
    public string Content { get; set; } = "";
}
