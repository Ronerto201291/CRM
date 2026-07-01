using Erp.Domain.Common;

namespace Erp.Modules.Crm.Domain.Entities;

public class Lead : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    /// <summary>Soft ref al Client creado al convertir este lead. null = no convertido aún.</summary>
    public Guid? ConvertedToClientId { get; set; }
}
