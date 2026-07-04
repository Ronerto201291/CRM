using MediatR;

namespace Erp.Modules.Treasury.Application.Features.CashSessions;

public record OpenCashSessionCommand(decimal OpeningBalance, string? Notes) : IRequest<CashSessionDto>;

public record CloseCashSessionCommand(Guid Id, decimal CountedClosingBalance, string? Notes) : IRequest<CashSessionDto>;

public record CashSessionDto(
    Guid Id,
    DateTime OpenedAt,
    decimal OpeningBalance,
    DateTime? ClosedAt,
    decimal? ExpectedClosingBalance,
    decimal? CountedClosingBalance,
    decimal? Difference,
    string Status,
    string? Notes);
