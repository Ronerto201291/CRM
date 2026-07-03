using Erp.Application.Common.Interfaces;
using Erp.Modules.Treasury.Application.Interfaces;
using Erp.Modules.Treasury.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Treasury.Application.Features.Financing;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record ConfirmingDto(
    Guid Id, Guid SupplierId, Guid InvoiceId, decimal InvoiceAmount,
    decimal AdvancePercentage, decimal AdvanceAmount, decimal Fee,
    DateTime CreationDate, DateTime DueDate, DateTime? PaymentDate,
    string Status, string FinancingProvider);

public record FactoringDto(
    Guid Id, Guid ClientId, Guid InvoiceId, decimal InvoiceAmount,
    decimal AdvancePercentage, decimal AdvanceAmount, decimal DiscountFee,
    decimal CommissionAmount, DateTime CreationDate, DateTime DueDate,
    DateTime? PaymentDate, string Status, string FactoringProvider, bool IsWithRecourse);

public record CreditLineDto(
    Guid Id, string Type, decimal Limit, decimal UtilizedAmount,
    decimal InterestRate, string Status, DateTime StartDate,
    DateTime ExpiryDate, string Provider);

// ── Queries / Commands ───────────────────────────────────────────────────────

public record GetConfirmingQuery(string? Status) : IRequest<IReadOnlyList<ConfirmingDto>>;

public record CreateConfirmingCommand(
    Guid SupplierId, Guid InvoiceId, decimal InvoiceAmount,
    decimal AdvancePercentage, decimal Fee, DateTime DueDate, string FinancingProvider)
    : IRequest<object>;

public record PayConfirmingCommand(Guid Id) : IRequest<object>;

public record GetFactoringQuery(string? Status) : IRequest<IReadOnlyList<FactoringDto>>;

public record CreateFactoringCommand(
    Guid ClientId, Guid InvoiceId, decimal InvoiceAmount,
    decimal AdvancePercentage, decimal DiscountFee, decimal CommissionAmount,
    DateTime DueDate, string FactoringProvider, bool IsWithRecourse)
    : IRequest<object>;

public record PayFactoringCommand(Guid Id) : IRequest<object>;

public record GetCreditLinesQuery : IRequest<IReadOnlyList<CreditLineDto>>;

public record CreateCreditLineCommand(
    string Type, decimal Limit, decimal InterestRate,
    DateTime StartDate, DateTime ExpiryDate, string Provider)
    : IRequest<object>;

// ── Handlers ─────────────────────────────────────────────────────────────────

public class GetConfirmingHandler : IRequestHandler<GetConfirmingQuery, IReadOnlyList<ConfirmingDto>>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GetConfirmingHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<ConfirmingDto>> Handle(GetConfirmingQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var query = _ctx.ConfirmingOperations.Where(c => c.CompanyId == tenantId);
        if (!string.IsNullOrEmpty(request.Status))
            query = query.Where(c => c.Status == request.Status);

        return await query.AsNoTracking()
            .Select(o => new ConfirmingDto(
                o.Id, o.SupplierId, o.InvoiceId, o.InvoiceAmount,
                o.AdvancePercentage, o.AdvanceAmount, o.Fee,
                o.CreationDate, o.DueDate, o.PaymentDate,
                o.Status, o.FinancingProvider))
            .ToListAsync(ct);
    }
}

public class CreateConfirmingHandler : IRequestHandler<CreateConfirmingCommand, object>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CreateConfirmingHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<object> Handle(CreateConfirmingCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var op = new ConfirmingOperation
        {
            Id = Guid.NewGuid(),
            CompanyId = tenantId,
            SupplierId = request.SupplierId,
            InvoiceId = request.InvoiceId,
            InvoiceAmount = request.InvoiceAmount,
            AdvancePercentage = request.AdvancePercentage,
            AdvanceAmount = request.InvoiceAmount * request.AdvancePercentage / 100m,
            Fee = request.Fee,
            CreationDate = DateTime.UtcNow,
            DueDate = request.DueDate,
            Status = "Active",
            FinancingProvider = request.FinancingProvider,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _ctx.ConfirmingOperations.Add(op);
        await _ctx.SaveChangesAsync(ct);
        return new { id = op.Id, status = op.Status };
    }
}

public class PayConfirmingHandler : IRequestHandler<PayConfirmingCommand, object>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public PayConfirmingHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<object> Handle(PayConfirmingCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var op = await _ctx.ConfirmingOperations
            .FirstOrDefaultAsync(o => o.Id == request.Id && o.CompanyId == tenantId, ct)
            ?? throw new KeyNotFoundException("Confirming operation not found");
        op.Status = "Paid";
        op.PaymentDate = DateTime.UtcNow;
        op.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        return new { id = op.Id, status = op.Status, paymentDate = op.PaymentDate };
    }
}

public class GetFactoringHandler : IRequestHandler<GetFactoringQuery, IReadOnlyList<FactoringDto>>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GetFactoringHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<FactoringDto>> Handle(GetFactoringQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var query = _ctx.FactoringOperations.Where(f => f.CompanyId == tenantId);
        if (!string.IsNullOrEmpty(request.Status))
            query = query.Where(f => f.Status == request.Status);

        return await query.AsNoTracking()
            .Select(o => new FactoringDto(
                o.Id, o.ClientId, o.InvoiceId, o.InvoiceAmount,
                o.AdvancePercentage, o.AdvanceAmount, o.DiscountFee,
                o.CommissionAmount, o.CreationDate, o.DueDate, o.PaymentDate,
                o.Status, o.FactoringProvider, o.IsWithRecourse))
            .ToListAsync(ct);
    }
}

public class CreateFactoringHandler : IRequestHandler<CreateFactoringCommand, object>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CreateFactoringHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<object> Handle(CreateFactoringCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var op = new FactoringOperation
        {
            Id = Guid.NewGuid(),
            CompanyId = tenantId,
            ClientId = request.ClientId,
            InvoiceId = request.InvoiceId,
            InvoiceAmount = request.InvoiceAmount,
            AdvancePercentage = request.AdvancePercentage,
            AdvanceAmount = request.InvoiceAmount * request.AdvancePercentage / 100m,
            DiscountFee = request.DiscountFee,
            CommissionAmount = request.CommissionAmount,
            CreationDate = DateTime.UtcNow,
            DueDate = request.DueDate,
            Status = "Active",
            FactoringProvider = request.FactoringProvider,
            IsWithRecourse = request.IsWithRecourse,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _ctx.FactoringOperations.Add(op);
        await _ctx.SaveChangesAsync(ct);
        return new { id = op.Id, status = op.Status };
    }
}

public class PayFactoringHandler : IRequestHandler<PayFactoringCommand, object>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public PayFactoringHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<object> Handle(PayFactoringCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var op = await _ctx.FactoringOperations
            .FirstOrDefaultAsync(f => f.Id == request.Id && f.CompanyId == tenantId, ct)
            ?? throw new KeyNotFoundException("Factoring operation not found");
        op.Status = "Paid";
        op.PaymentDate = DateTime.UtcNow;
        op.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        return new { id = op.Id, status = op.Status };
    }
}

public class GetCreditLinesHandler : IRequestHandler<GetCreditLinesQuery, IReadOnlyList<CreditLineDto>>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GetCreditLinesHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<CreditLineDto>> Handle(GetCreditLinesQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        return await _ctx.FinancingAccounts
            .Where(f => f.CompanyId == tenantId)
            .AsNoTracking()
            .Select(l => new CreditLineDto(
                l.Id, l.Type, l.Limit, l.UtilizedAmount,
                l.InterestRate, l.Status, l.StartDate, l.ExpiryDate, l.Provider))
            .ToListAsync(ct);
    }
}

public class CreateCreditLineHandler : IRequestHandler<CreateCreditLineCommand, object>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CreateCreditLineHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<object> Handle(CreateCreditLineCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var line = new FinancingAccount
        {
            Id = Guid.NewGuid(),
            CompanyId = tenantId,
            Type = request.Type,
            Limit = request.Limit,
            UtilizedAmount = 0,
            InterestRate = request.InterestRate,
            Status = "Active",
            StartDate = request.StartDate,
            ExpiryDate = request.ExpiryDate,
            Provider = request.Provider,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _ctx.FinancingAccounts.Add(line);
        await _ctx.SaveChangesAsync(ct);
        return new { id = line.Id };
    }
}
