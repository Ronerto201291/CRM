using Erp.Application.Common.Events;
using Erp.Application.Common.Interfaces;
using Erp.Modules.Treasury.Application.Interfaces;
using Erp.Modules.Treasury.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Treasury.Application.Features.Pos;

public record GetPosTerminalsQuery : IRequest<IReadOnlyList<PosTerminalDto>>;
public record CreatePosTerminalCommand(string Name, string TerminalCode) : IRequest<PosTerminalDto>;
public record RegisterPosPaymentCommand(Guid PosTerminalId, Guid InvoiceId, decimal? Amount, string? ExternalReference)
    : IRequest<PosPaymentDto>;

public sealed record PosTerminalDto(Guid Id, string Name, string TerminalCode, bool IsActive, DateTime? LastPaymentAt);
public sealed record PosPaymentDto(Guid Id, Guid PosTerminalId, Guid InvoiceId, decimal Amount, string CurrencyCode, DateTime PaidAt);

public class GetPosTerminalsHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    : IRequestHandler<GetPosTerminalsQuery, IReadOnlyList<PosTerminalDto>>
{
    public async Task<IReadOnlyList<PosTerminalDto>> Handle(GetPosTerminalsQuery request, CancellationToken ct)
        => await ctx.PosTerminals.AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new PosTerminalDto(t.Id, t.Name, t.TerminalCode, t.IsActive, t.LastPaymentAt))
            .ToListAsync(ct);
}

public class CreatePosTerminalHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    : IRequestHandler<CreatePosTerminalCommand, PosTerminalDto>
{
    public async Task<PosTerminalDto> Handle(CreatePosTerminalCommand request, CancellationToken ct)
    {
        var companyId = tenant.TenantId ?? throw new InvalidOperationException("No tenant context resolved.");
        var terminal = new PosTerminal
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Name = request.Name.Trim(),
            TerminalCode = request.TerminalCode.Trim(),
            IsActive = true,
        };
        ctx.PosTerminals.Add(terminal);
        await ctx.SaveChangesAsync(ct);
        return new PosTerminalDto(terminal.Id, terminal.Name, terminal.TerminalCode, terminal.IsActive, terminal.LastPaymentAt);
    }
}

public class RegisterPosPaymentHandler(
    ITreasuryDbContext ctx,
    ITenantContext tenant,
    IPublisher publisher) : IRequestHandler<RegisterPosPaymentCommand, PosPaymentDto>
{
    public async Task<PosPaymentDto> Handle(RegisterPosPaymentCommand request, CancellationToken ct)
    {
        var companyId = tenant.TenantId ?? throw new InvalidOperationException("No tenant context resolved.");

        var terminal = await ctx.PosTerminals.FirstOrDefaultAsync(t => t.Id == request.PosTerminalId && t.IsActive, ct)
            ?? throw new InvalidOperationException("Terminal TPV no encontrado o inactivo.");

        var amount = request.Amount ?? 0m;
        var payment = new PosPayment
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            PosTerminalId = terminal.Id,
            InvoiceId = request.InvoiceId,
            Amount = amount,
            CurrencyCode = "EUR",
            ExternalReference = request.ExternalReference,
            PaidAt = DateTime.UtcNow,
        };
        terminal.LastPaymentAt = payment.PaidAt;
        ctx.PosPayments.Add(payment);
        await ctx.SaveChangesAsync(ct);

        await publisher.Publish(new InvoiceCardPaymentRequestedEvent
        {
            InvoiceId = request.InvoiceId,
            CompanyId = companyId,
            PosTerminalId = terminal.Id,
            Amount = amount,
            ExternalReference = request.ExternalReference,
        }, ct);

        return new PosPaymentDto(payment.Id, payment.PosTerminalId, payment.InvoiceId, payment.Amount, payment.CurrencyCode, payment.PaidAt);
    }
}
