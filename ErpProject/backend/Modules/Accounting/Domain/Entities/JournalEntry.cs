using Erp.Domain.Common;

namespace Erp.Modules.Accounting.Domain.Entities;

/// <summary>Asiento contable con partida doble. ADR-0018 #14.</summary>
public class JournalEntry : BaseEntity
{
    public Guid CompanyId { get; set; }
    public DateTime Date { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? SourceType { get; set; }
    public Guid? SourceId { get; set; }
    public bool IsPosted { get; set; }
    public DateTime? PostedAt { get; set; }
    public ICollection<JournalEntryLine> JournalEntryLines { get; set; } = new List<JournalEntryLine>();
}
