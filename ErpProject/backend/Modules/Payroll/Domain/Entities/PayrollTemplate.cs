using Erp.Domain.Common;

namespace Erp.Modules.Payroll.Domain.Entities;

/// <summary>Plantilla de cálculo nómina por empresa (Fase 1).</summary>
public class PayrollTemplate : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Tipo contrato por defecto (Indefinido, Temporal…).</summary>
    public string DefaultContractType { get; set; } = "Indefinido";
    public decimal DefaultWeeklyHours { get; set; } = 40m;

    /// <summary>Porcentaje cuota obrera SS sobre base CC (ej. 6.35).</summary>
    public decimal EmployeeSsRatePercent { get; set; } = 6.35m;
    /// <summary>Porcentaje cuota patronal SS sobre base CC (ej. 30.0).</summary>
    public decimal EmployerSsRatePercent { get; set; } = 30.0m;
    /// <summary>Retención IRPF por defecto (%).</summary>
    public decimal DefaultIrpfRatePercent { get; set; } = 15m;

    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}
