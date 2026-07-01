using Erp.Domain.Common;

namespace Erp.Modules.Payroll.Domain.Entities;

/// <summary>Liquidación mensual de nómina (empresa).</summary>
public class PayrollSettlement : AuditableEntity
{
    public Guid CompanyId { get; set; }

    public int Year { get; set; }
    public int Month { get; set; }

    /// <summary>Draft | Final</summary>
    public string Status { get; set; } = "Draft";

    /// <summary>Asiento 640/642/476/4751/465 generado al cerrar la liquidación.</summary>
    public Guid? JournalEntryId { get; set; }

    public ICollection<PayrollLine> Lines { get; set; } = new List<PayrollLine>();
}
