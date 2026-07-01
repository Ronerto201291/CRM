using Erp.Domain.Common;

namespace Erp.Domain.Entities.Accounting;

/// <summary>
/// Single line in a journal entry.
/// Added AccountCode and AccountName for PGC (Plan General Contable) display.
/// </summary>
public class JournalEntryLine : BaseEntity
{
    public Guid JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }
    
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }
    
    // PGC fields for display/reports
    public string AccountCode { get; set; } = string.Empty;  // "430", "700", "472"
    public string AccountName { get; set; } = string.Empty;   // "Clientes", "Ventas"
    
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}
