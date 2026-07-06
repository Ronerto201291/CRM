using Erp.Domain.Common;

namespace Erp.Modules.Payroll.Domain.Entities;

/// <summary>Desglose cotización SS por línea (Fase 1).</summary>
public class SocialSecurityContribution : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid PayrollLineId { get; set; }
    public PayrollLine? PayrollLine { get; set; }

    /// <summary>CC, AT, Desempleo, Formación…</summary>
    public string ContingencyType { get; set; } = "CC";
    public decimal BaseAmount { get; set; }
    public decimal EmployeeRatePercent { get; set; }
    public decimal EmployerRatePercent { get; set; }
    public decimal EmployeeAmount { get; set; }
    public decimal EmployerAmount { get; set; }
}
