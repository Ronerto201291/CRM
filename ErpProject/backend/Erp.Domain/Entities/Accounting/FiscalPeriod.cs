namespace Erp.Domain.Entities.Accounting;

/// <summary>
/// Represents a closed fiscal year. Immutable once created.
/// Unique index per (CompanyId, FiscalYear) ensures one close per year.
/// Compliant with PGC español (RD 1514/2007).
/// </summary>
public class FiscalPeriod
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }

    /// <summary>The fiscal year that was closed (e.g. 2025).</summary>
    public int FiscalYear { get; set; }

    /// <summary>UTC timestamp when the period was closed.</summary>
    public DateTime ClosedAt { get; set; }

    /// <summary>User who executed the close (nullable if automated).</summary>
    public Guid? ClosedByUserId { get; set; }

    /// <summary>
    /// Id of the generated closing journal entry (asiento de cierre).
    /// Soft FK — no DB constraint to JournalEntries.
    /// </summary>
    public Guid ClosingJournalEntryId { get; set; }

    /// <summary>
    /// Id of the generated opening journal entry for the next fiscal year (asiento de apertura).
    /// Soft FK — no DB constraint to JournalEntries.
    /// </summary>
    public Guid OpeningJournalEntryId { get; set; }

    /// <summary>Net result (Resultado del Ejercicio) transferred to account 129.</summary>
    public decimal ResultadoNeto { get; set; }

    /// <summary>Summary description of what was closed.</summary>
    public string Notes { get; set; } = string.Empty;
}
