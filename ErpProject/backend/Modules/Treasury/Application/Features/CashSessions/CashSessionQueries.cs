using MediatR;

namespace Erp.Modules.Treasury.Application.Features.CashSessions;

public record GetOpenCashSessionQuery : IRequest<CashSessionDto?>;

public record GetCashSessionsQuery : IRequest<List<CashSessionDto>>;
