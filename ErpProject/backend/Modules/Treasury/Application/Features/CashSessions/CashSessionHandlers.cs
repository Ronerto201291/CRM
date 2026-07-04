using Erp.Application.Common.Events;
using Erp.Application.Common.Interfaces;
using Erp.Modules.Treasury.Application.Interfaces;
using Erp.Modules.Treasury.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Treasury.Application.Features.CashSessions;

internal static class CashSessionMapping
{
    public static CashSessionDto ToDto(CashSession s) => new(
        s.Id, s.OpenedAt, s.OpeningBalance, s.ClosedAt,
        s.ExpectedClosingBalance, s.CountedClosingBalance, s.Difference,
        s.Status, s.Notes);
}

public class OpenCashSessionHandler : IRequestHandler<OpenCashSessionCommand, CashSessionDto>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;
    private readonly IHttpContextCurrentUserAccessor _currentUser;

    public OpenCashSessionHandler(ITreasuryDbContext ctx, ITenantContext tenant, IHttpContextCurrentUserAccessor currentUser)
    {
        _ctx = ctx;
        _tenant = tenant;
        _currentUser = currentUser;
    }

    public async Task<CashSessionDto> Handle(OpenCashSessionCommand request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");

        var alreadyOpen = await _ctx.CashSessions.AnyAsync(s => s.Status == "Open", ct);
        if (alreadyOpen)
            throw new InvalidOperationException("Ya hay una caja abierta. Ciérrala antes de abrir otra.");

        if (request.OpeningBalance < 0)
            throw new InvalidOperationException("El importe de apertura no puede ser negativo.");

        var session = new CashSession
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            OpenedAt = DateTime.UtcNow,
            OpenedByUserId = _currentUser.UserId ?? Guid.Empty,
            OpeningBalance = request.OpeningBalance,
            Status = "Open",
            Notes = request.Notes,
        };
        _ctx.CashSessions.Add(session);
        await _ctx.SaveChangesAsync(ct);

        return CashSessionMapping.ToDto(session);
    }
}

public class CloseCashSessionHandler : IRequestHandler<CloseCashSessionCommand, CashSessionDto>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly IHttpContextCurrentUserAccessor _currentUser;
    private readonly IBankReconciliationLedgerQuery _ledger;
    private readonly ITenantContext _tenant;
    private readonly IPublisher _publisher;

    public CloseCashSessionHandler(
        ITreasuryDbContext ctx,
        IHttpContextCurrentUserAccessor currentUser,
        IBankReconciliationLedgerQuery ledger,
        ITenantContext tenant,
        IPublisher publisher)
    {
        _ctx = ctx;
        _currentUser = currentUser;
        _ledger = ledger;
        _tenant = tenant;
        _publisher = publisher;
    }

    public async Task<CashSessionDto> Handle(CloseCashSessionCommand request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");

        var session = await _ctx.CashSessions.FirstOrDefaultAsync(s => s.Id == request.Id, ct)
            ?? throw new InvalidOperationException("Caja no encontrada.");
        if (session.Status != "Open")
            throw new InvalidOperationException("Esta caja ya está cerrada.");

        var closedAt = DateTime.UtcNow;

        // Movimiento neto real en cuenta "570" (Caja) desde la apertura — reutiliza
        // IBankReconciliationLedgerQuery (ya usado por BankReconciliationService), sin
        // duplicar el acceso a IAccountingDbContext.
        var cashLines = await _ledger.GetPostedBankLinesAsync(companyId, "570", session.OpenedAt.Year, ct);
        var netMovement = cashLines
            .Where(l => l.EntryDate >= session.OpenedAt && l.EntryDate <= closedAt)
            .Sum(l => l.Debit - l.Credit);

        session.ExpectedClosingBalance = session.OpeningBalance + netMovement;
        session.CountedClosingBalance = request.CountedClosingBalance;
        session.Difference = request.CountedClosingBalance - session.ExpectedClosingBalance;
        session.ClosedAt = closedAt;
        session.ClosedByUserId = _currentUser.UserId ?? Guid.Empty;
        session.Status = "Closed";
        if (!string.IsNullOrWhiteSpace(request.Notes))
            session.Notes = string.IsNullOrWhiteSpace(session.Notes) ? request.Notes : $"{session.Notes} | {request.Notes}";

        await _ctx.SaveChangesAsync(ct);

        if (session.Difference != 0)
        {
            await _publisher.Publish(new CashSessionClosedEvent
            {
                CashSessionId = session.Id,
                CompanyId = companyId,
                Difference = session.Difference.Value,
                ClosedAt = closedAt,
            }, ct);
        }

        return CashSessionMapping.ToDto(session);
    }
}

public class GetOpenCashSessionHandler : IRequestHandler<GetOpenCashSessionQuery, CashSessionDto?>
{
    private readonly ITreasuryDbContext _ctx;
    public GetOpenCashSessionHandler(ITreasuryDbContext ctx) => _ctx = ctx;

    public async Task<CashSessionDto?> Handle(GetOpenCashSessionQuery request, CancellationToken ct)
    {
        var session = await _ctx.CashSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Status == "Open", ct);
        return session == null ? null : CashSessionMapping.ToDto(session);
    }
}

public class GetCashSessionsHandler : IRequestHandler<GetCashSessionsQuery, List<CashSessionDto>>
{
    private readonly ITreasuryDbContext _ctx;
    public GetCashSessionsHandler(ITreasuryDbContext ctx) => _ctx = ctx;

    public async Task<List<CashSessionDto>> Handle(GetCashSessionsQuery request, CancellationToken ct)
        => await _ctx.CashSessions
            .AsNoTracking()
            .OrderByDescending(s => s.OpenedAt)
            .Take(100)
            .Select(s => new CashSessionDto(
                s.Id, s.OpenedAt, s.OpeningBalance, s.ClosedAt,
                s.ExpectedClosingBalance, s.CountedClosingBalance, s.Difference,
                s.Status, s.Notes))
            .ToListAsync(ct);
}
