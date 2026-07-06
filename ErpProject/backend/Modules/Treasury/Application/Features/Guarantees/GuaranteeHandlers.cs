using Erp.Application.Common.Interfaces;
using Erp.Modules.Treasury.Application.Interfaces;
using Erp.Modules.Treasury.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Treasury.Application.Features.Guarantees;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record GuaranteeDto(
    Guid Id, string Type, string ReferenceNumber, decimal Amount, string CurrencyCode,
    string RelatedEntity, string Description, DateTime IssueDate, DateTime ExpiryDate,
    string Status, decimal ClaimedAmount, DateTime? ClaimDate);

public record CollateralDto(
    Guid Id, string Type, string Description, decimal Value,
    DateTime ValuationDate, string LinkedAccount, string Status, decimal LTVRatio);

public record BankGuaranteeDto(
    Guid Id, string GuaranteeNumber, string Bank, decimal Amount, string Type,
    DateTime IssuedDate, DateTime ExpiryDate, Guid? BeneficiaryId,
    string BeneficiaryName, decimal Fee, string Status);

// ── Queries / Commands ───────────────────────────────────────────────────────

public record GetGuaranteesQuery(string? Status) : IRequest<IReadOnlyList<GuaranteeDto>>;

public record CreateGuaranteeCommand(
    string Type, string ReferenceNumber, decimal Amount, string CurrencyCode,
    string RelatedEntity, string Description, DateTime IssueDate, DateTime ExpiryDate)
    : IRequest<object>;

public record ClaimGuaranteeCommand(Guid Id, decimal Amount) : IRequest<object>;

public record ReleaseGuaranteeCommand(Guid Id) : IRequest<object>;

public record GetCollateralQuery : IRequest<IReadOnlyList<CollateralDto>>;

public record CreateCollateralCommand(
    string Type, string Description, decimal Value, string LinkedAccount, decimal LTVRatio)
    : IRequest<object>;

public record GetBankGuaranteesQuery(string? Status) : IRequest<IReadOnlyList<BankGuaranteeDto>>;

public record CreateBankGuaranteeCommand(
    string GuaranteeNumber, string Bank, decimal Amount, string Type,
    DateTime IssuedDate, DateTime ExpiryDate, Guid? BeneficiaryId,
    string BeneficiaryName, decimal Fee) : IRequest<object>;

// ── Handlers ─────────────────────────────────────────────────────────────────

public class GetGuaranteesHandler : IRequestHandler<GetGuaranteesQuery, IReadOnlyList<GuaranteeDto>>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GetGuaranteesHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<GuaranteeDto>> Handle(GetGuaranteesQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var query = _ctx.Guarantees.Where(g => g.CompanyId == tenantId);
        if (!string.IsNullOrEmpty(request.Status))
            query = query.Where(g => g.Status == request.Status);

        return await query.AsNoTracking()
            .Select(g => new GuaranteeDto(
                g.Id, g.Type, g.ReferenceNumber, g.Amount, g.CurrencyCode,
                g.RelatedEntity, g.Description, g.IssueDate, g.ExpiryDate,
                g.Status, g.ClaimedAmount, g.ClaimDate))
            .ToListAsync(ct);
    }
}

public class CreateGuaranteeHandler : IRequestHandler<CreateGuaranteeCommand, object>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CreateGuaranteeHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<object> Handle(CreateGuaranteeCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var guarantee = new Guarantee
        {
            Id = Guid.NewGuid(),
            CompanyId = tenantId,
            Type = request.Type,
            ReferenceNumber = request.ReferenceNumber,
            Amount = request.Amount,
            CurrencyCode = request.CurrencyCode,
            RelatedEntity = request.RelatedEntity,
            Description = request.Description,
            IssueDate = request.IssueDate,
            ExpiryDate = request.ExpiryDate,
            Status = "Active",
            ClaimedAmount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _ctx.Guarantees.Add(guarantee);
        await _ctx.SaveChangesAsync(ct);
        return new { id = guarantee.Id, status = guarantee.Status };
    }
}

public class ClaimGuaranteeHandler : IRequestHandler<ClaimGuaranteeCommand, object>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public ClaimGuaranteeHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<object> Handle(ClaimGuaranteeCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var g = await _ctx.Guarantees
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.CompanyId == tenantId, ct)
            ?? throw new KeyNotFoundException("Guarantee not found");
        g.Status = "Claimed";
        g.ClaimedAmount = request.Amount;
        g.ClaimDate = DateTime.UtcNow;
        g.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        return new { id = g.Id, status = g.Status, g.ClaimedAmount };
    }
}

public class ReleaseGuaranteeHandler : IRequestHandler<ReleaseGuaranteeCommand, object>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public ReleaseGuaranteeHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<object> Handle(ReleaseGuaranteeCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var g = await _ctx.Guarantees
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.CompanyId == tenantId, ct)
            ?? throw new KeyNotFoundException("Guarantee not found");
        g.Status = "Released";
        g.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        return new { id = g.Id, status = g.Status };
    }
}

public class GetCollateralHandler : IRequestHandler<GetCollateralQuery, IReadOnlyList<CollateralDto>>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GetCollateralHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<CollateralDto>> Handle(GetCollateralQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        return await _ctx.Collaterals
            .Where(c => c.CompanyId == tenantId)
            .AsNoTracking()
            .Select(c => new CollateralDto(
                c.Id, c.Type, c.Description, c.Value,
                c.ValuationDate, c.LinkedAccount, c.Status, c.LTVRatio))
            .ToListAsync(ct);
    }
}

public class CreateCollateralHandler : IRequestHandler<CreateCollateralCommand, object>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CreateCollateralHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<object> Handle(CreateCollateralCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var collateral = new Collateral
        {
            Id = Guid.NewGuid(),
            CompanyId = tenantId,
            Type = request.Type,
            Description = request.Description,
            Value = request.Value,
            ValuationDate = DateTime.UtcNow,
            LinkedAccount = request.LinkedAccount,
            Status = "Pledged",
            LTVRatio = request.LTVRatio,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _ctx.Collaterals.Add(collateral);
        await _ctx.SaveChangesAsync(ct);
        return new { id = collateral.Id };
    }
}

public class GetBankGuaranteesHandler : IRequestHandler<GetBankGuaranteesQuery, IReadOnlyList<BankGuaranteeDto>>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GetBankGuaranteesHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<BankGuaranteeDto>> Handle(GetBankGuaranteesQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var query = _ctx.BankGuarantees.Where(b => b.CompanyId == tenantId);
        if (!string.IsNullOrEmpty(request.Status))
            query = query.Where(b => b.Status == request.Status);

        return await query.AsNoTracking()
            .Select(g => new BankGuaranteeDto(
                g.Id, g.GuaranteeNumber, g.Bank, g.Amount, g.Type,
                g.IssuedDate, g.ExpiryDate, g.BeneficiaryId,
                g.BeneficiaryName, g.Fee, g.Status))
            .ToListAsync(ct);
    }
}

public class CreateBankGuaranteeHandler : IRequestHandler<CreateBankGuaranteeCommand, object>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CreateBankGuaranteeHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<object> Handle(CreateBankGuaranteeCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var bg = new BankGuarantee
        {
            Id = Guid.NewGuid(),
            CompanyId = tenantId,
            GuaranteeNumber = request.GuaranteeNumber,
            Bank = request.Bank,
            Amount = request.Amount,
            Type = request.Type,
            IssuedDate = request.IssuedDate,
            ExpiryDate = request.ExpiryDate,
            BeneficiaryId = request.BeneficiaryId,
            BeneficiaryName = request.BeneficiaryName,
            Fee = request.Fee,
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _ctx.BankGuarantees.Add(bg);
        await _ctx.SaveChangesAsync(ct);
        return new { id = bg.Id };
    }
}
