namespace Erp.Modules.Accounting.Domain.Entities;

/// <summary>
/// Período fiscal cerrado. ADR-0018 #14: entidad migrada desde Erp.Domain.
/// </summary>
public class FiscalPeriod
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public int FiscalYear { get; set; }
    public DateTime ClosedAt { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public Guid ClosingJournalEntryId { get; set; }
    public Guid OpeningJournalEntryId { get; set; }
    public decimal ResultadoNeto { get; set; }
    public string Notes { get; set; } = string.Empty;
}
