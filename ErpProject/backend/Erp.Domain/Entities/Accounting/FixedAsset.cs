using Erp.Domain.Common;

namespace Erp.Domain.Entities.Accounting;

/// <summary>
/// Activo fijo sujeto a amortización (PGC grupos 21/22).
/// Motor de amortización lineal automático vía Hangfire (dotación mensual).
///
/// Asiento de dotación mensual:
///   Debe  68x  Dotación amortización inmovilizado    = cuota mensual
///   Haber 28x  Amortización acumulada inmovilizado   = cuota mensual
///
/// Tablas TRLIS / RD 1777/2004 (referencia):
///   Edificios: 33 años | Maquinaria: 15 años | Vehículos: 10 años
///   Equipos informáticos: 6 años | Mobiliario: 10 años
/// </summary>
public class FixedAsset : AuditableEntity
{
    public Guid CompanyId { get; set; }

    /// <summary>Código identificador interno del activo (ej. "VEH-001", "INF-002")</summary>
    public string AssetCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Fecha de adquisición/compra del activo</summary>
    public DateTime AcquisitionDate { get; set; }

    /// <summary>Fecha de puesta en funcionamiento — desde aquí se computa la amortización</summary>
    public DateTime CommissioningDate { get; set; }

    /// <summary>Coste de adquisición (precio + gastos inherentes a la compra)</summary>
    public decimal AcquisitionCost { get; set; }

    /// <summary>Valor residual estimado al final de la vida útil (normalmente 0)</summary>
    public decimal ResidualValue { get; set; }

    /// <summary>Vida útil estimada en años (ver tablas TRLIS)</summary>
    public int UsefulLifeYears { get; set; }

    /// <summary>Método de amortización: "Lineal" (cuota fija mensual)</summary>
    public string AmortizationMethod { get; set; } = "Lineal";

    /// <summary>Cuenta de activo PGC (210/211/213/214/215/216). Por defecto: 213 Maquinaria</summary>
    public string AssetAccountCode { get; set; } = "213";

    /// <summary>Cuenta dotación amortización (680/681/682). Por defecto: 681 Inmovilizado material</summary>
    public string DepreciationAccountCode { get; set; } = "681";

    /// <summary>Cuenta amortización acumulada (280/281/282). Por defecto: 281</summary>
    public string AccumDepreciationAccountCode { get; set; } = "281";

    /// <summary>Amortización acumulada hasta la fecha (suma de todas las dotaciones)</summary>
    public decimal AccumulatedDepreciation { get; set; }

    /// <summary>Fecha del último asiento de dotación generado</summary>
    public DateTime? LastAmortizationDate { get; set; }

    /// <summary>Estado del activo: Active | FullyAmortized | Disposed</summary>
    public string Status { get; set; } = "Active";

    /// <summary>Fecha de baja (enajenación o pérdida del activo)</summary>
    public DateTime? DisposedAt { get; set; }

    public string? Notes { get; set; }

    // ── Calculated (not persisted) ───────────────────────────────────────────
    public decimal DepreciableAmount  => AcquisitionCost - ResidualValue;
    public decimal MonthlyDepreciation => UsefulLifeYears > 0 && DepreciableAmount > 0
        ? DepreciableAmount / (UsefulLifeYears * 12m)
        : 0m;
    public decimal NetBookValue => AcquisitionCost - AccumulatedDepreciation;
}
