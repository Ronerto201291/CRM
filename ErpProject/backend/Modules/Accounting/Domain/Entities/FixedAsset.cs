using Erp.Domain.Common;

namespace Erp.Modules.Accounting.Domain.Entities;

/// <summary>
/// Activo fijo sujeto a amortización (PGC grupos 21/22). ADR-0018 #14.
/// </summary>
public class FixedAsset : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime AcquisitionDate { get; set; }
    public DateTime CommissioningDate { get; set; }
    public decimal AcquisitionCost { get; set; }
    public decimal ResidualValue { get; set; }
    public int UsefulLifeYears { get; set; }
    public string AmortizationMethod { get; set; } = "Lineal";
    public string AssetAccountCode { get; set; } = "213";
    public string DepreciationAccountCode { get; set; } = "681";
    public string AccumDepreciationAccountCode { get; set; } = "281";
    public decimal AccumulatedDepreciation { get; set; }
    public DateTime? LastAmortizationDate { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime? DisposedAt { get; set; }
    public string? Notes { get; set; }

    public decimal DepreciableAmount => AcquisitionCost - ResidualValue;
    public decimal MonthlyDepreciation => UsefulLifeYears > 0 && DepreciableAmount > 0
        ? DepreciableAmount / (UsefulLifeYears * 12m)
        : 0m;
    public decimal NetBookValue => AcquisitionCost - AccumulatedDepreciation;
}
