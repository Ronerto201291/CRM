using Erp.Domain.Common;

namespace Erp.Modules.Crm.Domain.Entities;

public class ScheduledAlert : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTime ScheduledAt { get; set; }
    public Guid? ClientId { get; set; }
    public string? ClientName { get; set; }
    public bool IsAcknowledged { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? SnoozedUntil { get; set; }
}
