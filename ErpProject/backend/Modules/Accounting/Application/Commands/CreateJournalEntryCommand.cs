using Erp.Application.DTOs;
using MediatR;

namespace Erp.Modules.Accounting.Application.Commands;

public class CreateJournalEntryCommand : IRequest<JournalEntryDto>
{
    public string Reference { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public List<JournalLineInput> Lines { get; set; } = new();
}

public class JournalLineInput
{
    public Guid AccountId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}
