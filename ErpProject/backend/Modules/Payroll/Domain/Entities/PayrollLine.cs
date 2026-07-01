using Erp.Domain.Common;

namespace Erp.Modules.Payroll.Domain.Entities;

/// <summary>Línea de nómina por trabajador en un mes.</summary>
public class PayrollLine : AuditableEntity
{
    public Guid PayrollSettlementId { get; set; }
    public PayrollSettlement? Settlement { get; set; }

    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public decimal GrossSalary { get; set; }
    /// <summary>Base cotización contingencias comunes (TGSS).</summary>
    public decimal CommonContingenciesBase { get; set; }
    /// <summary>Cuota obrera SS (trabajador).</summary>
    public decimal EmployeeSocialSecurity { get; set; }
    /// <summary>Cuota patronal SS (empresa).</summary>
    public decimal EmployerSocialSecurity { get; set; }
    /// <summary>Base retención IRPF.</summary>
    public decimal IrpfBase { get; set; }
    public decimal IrpfRate { get; set; }
    public decimal IrpfWithheld { get; set; }
    public decimal NetPay { get; set; }
}
