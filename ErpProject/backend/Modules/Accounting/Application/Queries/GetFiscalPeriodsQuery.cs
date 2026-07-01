using Erp.Domain.Entities.Accounting;
using Erp.Modules.Accounting.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Application.Queries;

public record GetFiscalPeriodsQuery : IRequest<List<FiscalPeriodDto>>;

public record FiscalPeriodDto(
    Guid Id,
    int FiscalYear,
    DateTime ClosedAt,
    decimal ResultadoNeto,
    Guid ClosingJournalEntryId,
    string Notes);

public class GetFiscalPeriodsHandler : IRequestHandler<GetFiscalPeriodsQuery, List<FiscalPeriodDto>>
{
    private readonly IAccountingDbContext _ctx;
    public GetFiscalPeriodsHandler(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task<List<FiscalPeriodDto>> Handle(GetFiscalPeriodsQuery req, CancellationToken ct)
        => await _ctx.FiscalPeriods
            .OrderByDescending(p => p.FiscalYear)
            .Select(p => new FiscalPeriodDto(
                p.Id, p.FiscalYear, p.ClosedAt,
                p.ResultadoNeto, p.ClosingJournalEntryId, p.Notes))
            .ToListAsync(ct);
}
