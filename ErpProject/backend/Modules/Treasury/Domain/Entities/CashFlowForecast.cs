using Erp.Domain.Common;

namespace Erp.Modules.Treasury.Domain.Entities;

/// <summary>
/// Previsión de flujo de caja (cash flow forecast).
/// Auto-generada desde facturas/expenses pendientes o introducida manualmente.
/// </summary>
public class CashFlowForecast : AuditableEntity
{
    public Guid CompanyId { get; set; }

    /// <summary>Fecha para la que se predice el flujo</summary>
    public DateTime ForecastDate { get; set; }

    /// <summary>Entradas de efectivo previstas (cobros)</summary>
    public decimal ExpectedInflow { get; set; }

    /// <summary>Salidas de efectivo previstas (pagos)</summary>
    public decimal ExpectedOutflow { get; set; }

    /// <summary>Saldo previsto (saldo anterior + inflow - outflow)</summary>
    public decimal ExpectedBalance { get; set; }

    /// <summary>Origen de la previsión: Invoice | Expense | Manual | OpeningBalance</summary>
    public string Source { get; set; } = "Manual";

    /// <summary>ID del documento origen (InvoiceId o ExpenseUploadId)</summary>
    public Guid? SourceId { get; set; }

    /// <summary>Si ya es un dato real (realizado) vs previsão</summary>
    public bool IsActual { get; set; }

    /// <summary>Notas</summary>
    public string? Notes { get; set; }
}
