using Erp.Domain.Common;

namespace Erp.Modules.Accounting.Domain.Entities;

/// <summary>Línea de asiento contable. ADR-0018 #14.</summary>
public class JournalEntryLine : BaseEntity
{
    public Guid JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}
