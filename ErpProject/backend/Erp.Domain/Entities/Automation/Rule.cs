using Erp.Domain.Common;

namespace Erp.Domain.Entities.Automation;

public class Rule : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    // e.g., "OnInvoiceCreated", "OnLeadStatusChanged"
    public string TriggerEvent { get; set; } = string.Empty; 
    
    public bool IsActive { get; set; } = true;

    public ICollection<Condition> Conditions { get; set; } = new List<Condition>();
    public ICollection<Action> Actions { get; set; } = new List<Action>();
}
