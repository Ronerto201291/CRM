using Erp.Application.DTOs;
using Erp.Modules.Accounting.Application.Commands;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Accounting.Application.Queries;
using Erp.Domain.Entities.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Application.Handlers;

public class CreateJournalEntryHandler : IRequestHandler<CreateJournalEntryCommand, JournalEntryDto>
{
    private readonly IAccountingDbContext _ctx;
    public CreateJournalEntryHandler(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task<JournalEntryDto> Handle(CreateJournalEntryCommand req, CancellationToken ct)
    {
        var entry = new JournalEntry { Id = Guid.NewGuid(), Date = req.Date, Reference = req.Reference };
        _ctx.JournalEntries.Add(entry);
        var lines = req.Lines.Select(l => new JournalEntryLine
        {
            Id = Guid.NewGuid(),
            JournalEntryId = entry.Id,
            AccountId = l.AccountId,
            Debit = l.Debit,
            Credit = l.Credit
        }).ToList();
        foreach (var l in lines) _ctx.JournalEntryLines.Add(l);
        await _ctx.SaveChangesAsync(ct);
        return new JournalEntryDto
        {
            Id = entry.Id, Date = entry.Date, Reference = entry.Reference,
            Lines = lines.Select(l => new JournalEntryLineDto
            {
                Id = l.Id, AccountId = l.AccountId, Debit = l.Debit, Credit = l.Credit
            }).ToList()
        };
    }
}

public class GetJournalEntriesHandler : IRequestHandler<GetJournalEntriesQuery, List<JournalEntryDto>>
{
    private readonly IAccountingDbContext _ctx;
    public GetJournalEntriesHandler(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task<List<JournalEntryDto>> Handle(GetJournalEntriesQuery req, CancellationToken ct)
    {
        return await _ctx.JournalEntries.Include(j => j.JournalEntryLines).Select(j => new JournalEntryDto
        {
            Id = j.Id, Date = j.Date, Reference = j.Reference,
            Lines = j.JournalEntryLines.Select(l => new JournalEntryLineDto
            {
                Id = l.Id, AccountId = l.AccountId, Debit = l.Debit, Credit = l.Credit
            }).ToList()
        }).ToListAsync(ct);
    }
}
