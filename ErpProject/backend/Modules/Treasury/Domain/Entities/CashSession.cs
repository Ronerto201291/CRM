using Erp.Domain.Common;

namespace Erp.Modules.Treasury.Domain.Entities;

/// <summary>
/// Arqueo de caja (ADR-0018 #42b): apertura con importe inicial, cierre con
/// importe esperado (calculado a partir de los movimientos reales en cuenta
/// "570" desde la apertura) vs. importe contado por el usuario. La
/// diferencia, si la hay, se publica como CashSessionClosedEvent para que
/// Accounting registre el ajuste contable (668/778) — Treasury no crea
/// asientos contables directamente.
/// </summary>
public class CashSession : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public DateTime OpenedAt { get; set; }
    public Guid OpenedByUserId { get; set; }
    public decimal OpeningBalance { get; set; }

    public DateTime? ClosedAt { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public decimal? ExpectedClosingBalance { get; set; }
    public decimal? CountedClosingBalance { get; set; }
    /// <summary>CountedClosingBalance - ExpectedClosingBalance. Positivo = sobra, negativo = falta.</summary>
    public decimal? Difference { get; set; }

    /// <summary>"Open" | "Closed"</summary>
    public string Status { get; set; } = "Open";
    public string? Notes { get; set; }
}
