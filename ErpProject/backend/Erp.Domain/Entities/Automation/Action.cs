using Erp.Domain.Common;

namespace Erp.Domain.Entities.Automation;

public class Action : BaseEntity
{
    public Guid RuleId { get; set; }
    public Rule? Rule { get; set; }
    
    public string Type { get; set; } = string.Empty; // e.g., "SendEmail", "CreateTask"
    public string Configuration { get; set; } = "{}"; // JSONB
    public int ExecutionOrder { get; set; }
}
