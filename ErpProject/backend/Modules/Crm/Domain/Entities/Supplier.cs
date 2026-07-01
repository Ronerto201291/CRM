using Erp.Domain.Common;

namespace Erp.Modules.Crm.Domain.Entities;

public class Supplier : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty; // CIF/NIF
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? BankAccount { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ActivityLog> Activities { get; set; } = new List<ActivityLog>();

    // RGPD — derecho de supresión (pseudoanonimización)
    public bool IsAnonymized { get; set; } = false;
    public DateTime? AnonymizedAt { get; set; }
}
