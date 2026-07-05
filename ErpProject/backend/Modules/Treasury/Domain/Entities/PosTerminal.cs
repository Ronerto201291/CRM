using Erp.Domain.Common;

namespace Erp.Modules.Treasury.Domain.Entities;

/// <summary>Terminal físico TPV vinculado a cobros con tarjeta (ADR-0018 #41 mínimo viable).</summary>
public class PosTerminal : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Identificador del terminal en la pasarela o TPV físico.</summary>
    public string TerminalCode { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime? LastPaymentAt { get; set; }
}
