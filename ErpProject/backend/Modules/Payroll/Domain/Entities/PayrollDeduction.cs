using Erp.Domain.Common;

namespace Erp.Modules.Payroll.Domain.Entities;

/// <summary>Deducción/descontable en línea de nómina (Fase 1).</summary>
public class PayrollDeduction : AuditableEntity
{
    public Guid PayrollLineId { get; set; }
    public PayrollLine? PayrollLine { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    /// <summary>Devengo, Deduccion, RetencionIrpf, CuotaSs…</summary>
    public string Category { get; set; } = "Deduccion";
}
