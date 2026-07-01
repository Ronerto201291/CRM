using Erp.Domain.Common;

namespace Erp.Domain.Entities.Automation;

public class Condition : BaseEntity
{
    public Guid RuleId { get; set; }
    public Rule? Rule { get; set; }
    
    public string Field { get; set; } = string.Empty; // e.g., "Total"
    public string Operator { get; set; } = string.Empty; // e.g., ">"
    public string Value { get; set; } = string.Empty; // e.g., "1000"
}
