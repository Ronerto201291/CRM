namespace Erp.Application.DTOs;
public class JournalEntryDto
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public string Reference { get; set; } = string.Empty;
    public List<JournalEntryLineDto> Lines { get; set; } = new();
}
public class JournalEntryLineDto
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}
