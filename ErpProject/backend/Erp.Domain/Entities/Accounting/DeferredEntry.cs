using Erp.Domain.Common;

namespace Erp.Domain.Entities.Accounting;

/// <summary>
/// Periodificación contable (PGC cuentas 480 / 485).
///
/// 480 Gastos anticipados — pago realizado este período cuyo gasto corresponde al siguiente.
///   Asiento de registro inicial:
///     Debe  480  Gastos anticipados = TotalAmount
///     Haber 6xx  Cuenta de gasto    = TotalAmount   (compensación del gasto adelantado)
///   Asiento de reconocimiento mensual:
///     Debe  6xx  Cuenta de gasto    = cuota mensual
///     Haber 480  Gastos anticipados = cuota mensual
///
/// 485 Ingresos anticipados — cobro recibido cuyo ingreso pertenece al siguiente período.
///   Asiento de registro inicial:
///     Debe  57x  Tesorería          = TotalAmount
///     Haber 485  Ingresos anticipados = TotalAmount
///   Asiento de reconocimiento mensual:
///     Debe  485  Ingresos anticipados = cuota mensual
///     Haber 7xx  Cuenta de ingreso    = cuota mensual
/// </summary>
public class DeferredEntry : AuditableEntity
{
    public Guid CompanyId { get; set; }

    /// <summary>Tipo: "PrepaidExpense" (480) | "DeferredRevenue" (485)</summary>
    public string EntryType { get; set; } = "PrepaidExpense";

    public string Description { get; set; } = string.Empty;

    /// <summary>Importe total a periodificar</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Inicio del período de devengo (primer mes a reconocer)</summary>
    public DateTime PeriodStart { get; set; }

    /// <summary>Fin del período de devengo (último mes a reconocer)</summary>
    public DateTime PeriodEnd { get; set; }

    /// <summary>Importe ya reconocido en resultados por los asientos mensuales</summary>
    public decimal RecognizedAmount { get; set; }

    /// <summary>Estado: Active | Completed</summary>
    public string Status { get; set; } = "Active";

    /// <summary>Cuenta de periodificación: 480 (gastos anticipados) o 485 (ingresos anticipados)</summary>
    public string DeferralAccountCode { get; set; } = "480";

    /// <summary>Cuenta de resultado contrapartida: 6xx para gastos, 7xx para ingresos</summary>
    public string CounterpartAccountCode { get; set; } = string.Empty;

    /// <summary>Tipo del documento de origen (soft FK): "Invoice" | "Expense" | null</summary>
    public string? SourceType { get; set; }

    /// <summary>Id del documento de origen (soft FK)</summary>
    public Guid? SourceId { get; set; }

    // ── Calculated (not persisted) ───────────────────────────────────────────
    public decimal RemainingAmount => TotalAmount - RecognizedAmount;
    public int TotalMonths => Math.Max(1,
        ((PeriodEnd.Year - PeriodStart.Year) * 12) + (PeriodEnd.Month - PeriodStart.Month) + 1);
    public decimal MonthlyAmount => TotalMonths > 0 ? TotalAmount / TotalMonths : 0m;
}
