using Erp.Domain.Common;

namespace Erp.Modules.Crm.Domain.Entities;

public class Contact : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    
    // Can link to Client or Supplier
    public Guid? ClientId { get; set; }
    public Client? Client { get; set; }
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    // RGPD — derecho de supresión (pseudoanonimización)
    public bool IsAnonymized { get; set; } = false;
    public DateTime? AnonymizedAt { get; set; }
}
