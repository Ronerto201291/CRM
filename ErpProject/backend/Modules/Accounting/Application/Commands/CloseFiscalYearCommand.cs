using MediatR;

namespace Erp.Modules.Accounting.Application.Commands;

public record CloseFiscalYearCommand(int FiscalYear) : IRequest<CloseFiscalYearResult>;

public record CloseFiscalYearResult(
    Guid FiscalPeriodId,
    Guid ClosingJournalEntryId,
    Guid OpeningJournalEntryId,
    decimal ResultadoNeto,
    string Message);
