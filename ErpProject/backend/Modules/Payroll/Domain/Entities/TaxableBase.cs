using Erp.Domain.Common;

namespace Erp.Modules.Payroll.Domain.Entities;

/// <summary>Base imponible IRPF por línea (Fase 1).</summary>
public class TaxableBase : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid PayrollLineId { get; set; }
    public PayrollLine? PayrollLine { get; set; }

    /// <summary>General, Especie, Exenta…</summary>
    public string BaseType { get; set; } = "General";
    public decimal Amount { get; set; }
    public decimal IrpfRatePercent { get; set; }
    public decimal IrpfWithheld { get; set; }
}
