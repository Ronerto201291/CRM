using Erp.Domain.Common;

namespace Erp.Domain.Entities.Accounting;

/// <summary>
/// Journal entry with double-entry accounting enforcement.
/// Once posted, cannot be modified or deleted.
/// Links to source document via SourceType + SourceId.
/// </summary>
public class JournalEntry : BaseEntity
{
    public Guid CompanyId { get; set; }
    public DateTime Date { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    // Source document linkage
    public string? SourceType { get; set; }   // "Invoice", "Expense"
    public Guid? SourceId { get; set; }        // FK to source document
    
    // Posting control
    public bool IsPosted { get; set; }
    public DateTime? PostedAt { get; set; }
    
    public ICollection<JournalEntryLine> JournalEntryLines { get; set; } = new List<JournalEntryLine>();
}
