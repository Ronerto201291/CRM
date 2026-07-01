using Erp.Domain.Common;

namespace Erp.Modules.Crm.Domain.Entities;

/// <summary>
/// Timeline log of important CRM actions.
/// </summary>
public class ActivityLog : BaseEntity
{
    public Guid CompanyId { get; set; }
    public Guid? UserId { get; set; }
    public string EntityType { get; set; } = string.Empty; // "Client", "Supplier", "Invoice", "Expense"
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty; // "Created", "Updated", "InvoiceIssued", "ExpenseUploaded"
    public string? Description { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
