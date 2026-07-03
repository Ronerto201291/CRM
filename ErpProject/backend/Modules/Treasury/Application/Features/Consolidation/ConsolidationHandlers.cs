using Erp.Application.Common.Interfaces;
using Erp.Modules.Treasury.Application.Interfaces;
using Erp.Modules.Treasury.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Treasury.Application.Features.Consolidation;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record ConsolidationGroupDto(
    Guid Id, string Name, string Code, Guid ParentCompanyId,
    decimal ConsolidationPercentage, DateTime ConsolidationDate,
    string Method, string Status);

public record SubsidiaryDto(
    Guid Id, Guid CompanyId, Guid ParentCompanyId,
    decimal OwnershipPercentage, decimal VotingPercentage,
    string ConsolidationMethod, DateTime AcquisitionDate,
    decimal AcquisitionPrice, string Status, DateTime? DisposalDate);

public record ConsolidatedStatementDto(
    Guid Id, int FiscalYear, string StatementType,
    decimal TotalRevenue, decimal TotalExpenses, decimal NetIncome,
    decimal TotalAssets, decimal TotalLiabilities, decimal TotalEquity,
    DateTime PreparedDate, string Status);

public record IntercompanyTransactionDto(
    Guid Id, Guid ParentCompanyId, Guid SubsidiaryId,
    string Type, decimal Amount, string Currency,
    DateTime TransactionDate, string Status, bool IsEliminated,
    Guid? RelatedInvoiceId);

// ── Queries / Commands ───────────────────────────────────────────────────────

public record GetConsolidationGroupsQuery : IRequest<IReadOnlyList<ConsolidationGroupDto>>;

public record CreateConsolidationGroupCommand(
    string Name, string Code, decimal ConsolidationPercentage, string Method)
    : IRequest<object>;

public record GetSubsidiariesQuery(Guid GroupId) : IRequest<IReadOnlyList<SubsidiaryDto>>;

public record AddSubsidiaryCommand(
    Guid GroupId, Guid CompanyId, decimal OwnershipPercentage,
    decimal VotingPercentage, string ConsolidationMethod,
    DateTime AcquisitionDate, decimal AcquisitionPrice)
    : IRequest<object>;

public record GetConsolidatedStatementsQuery(Guid GroupId) : IRequest<IReadOnlyList<ConsolidatedStatementDto>>;

public record ConsolidateGroupCommand(Guid GroupId) : IRequest<object>;

public record GetIntercompanyTransactionsQuery(Guid GroupId) : IRequest<IReadOnlyList<IntercompanyTransactionDto>>;

public record EliminateIntercompanyCommand(Guid GroupId) : IRequest<object>;

// ── Handlers ─────────────────────────────────────────────────────────────────

public class GetConsolidationGroupsHandler : IRequestHandler<GetConsolidationGroupsQuery, IReadOnlyList<ConsolidationGroupDto>>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GetConsolidationGroupsHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<ConsolidationGroupDto>> Handle(GetConsolidationGroupsQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        return await _ctx.ConsolidationGroups
            .Where(g => g.ParentCompanyId == tenantId)
            .AsNoTracking()
            .Select(g => new ConsolidationGroupDto(
                g.Id, g.Name, g.Code, g.ParentCompanyId,
                g.ConsolidationPercentage, g.ConsolidationDate, g.Method, g.Status))
            .ToListAsync(ct);
    }
}

public class CreateConsolidationGroupHandler : IRequestHandler<CreateConsolidationGroupCommand, object>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CreateConsolidationGroupHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<object> Handle(CreateConsolidationGroupCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var group = new ConsolidationGroup
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Code = request.Code,
            ParentCompanyId = tenantId,
            ConsolidationPercentage = request.ConsolidationPercentage,
            ConsolidationDate = DateTime.UtcNow,
            Method = request.Method,
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _ctx.ConsolidationGroups.Add(group);
        await _ctx.SaveChangesAsync(ct);
        return new { id = group.Id, status = group.Status };
    }
}

public class GetSubsidiariesHandler : IRequestHandler<GetSubsidiariesQuery, IReadOnlyList<SubsidiaryDto>>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GetSubsidiariesHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<SubsidiaryDto>> Handle(GetSubsidiariesQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        return await _ctx.SubsidiaryCompanies
            .Where(s => s.ParentCompanyId == tenantId)
            .AsNoTracking()
            .Select(s => new SubsidiaryDto(
                s.Id, s.CompanyId, s.ParentCompanyId,
                s.OwnershipPercentage, s.VotingPercentage,
                s.ConsolidationMethod, s.AcquisitionDate,
                s.AcquisitionPrice, s.Status, s.DisposalDate))
            .ToListAsync(ct);
    }
}

public class AddSubsidiaryHandler : IRequestHandler<AddSubsidiaryCommand, object>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public AddSubsidiaryHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<object> Handle(AddSubsidiaryCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var sub = new SubsidiaryCompany
        {
            Id = Guid.NewGuid(),
            CompanyId = request.CompanyId,
            ParentCompanyId = tenantId,
            OwnershipPercentage = request.OwnershipPercentage,
            VotingPercentage = request.VotingPercentage,
            ConsolidationMethod = request.ConsolidationMethod,
            AcquisitionDate = request.AcquisitionDate,
            AcquisitionPrice = request.AcquisitionPrice,
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _ctx.SubsidiaryCompanies.Add(sub);
        await _ctx.SaveChangesAsync(ct);
        return new { id = sub.Id };
    }
}

public class GetConsolidatedStatementsHandler : IRequestHandler<GetConsolidatedStatementsQuery, IReadOnlyList<ConsolidatedStatementDto>>
{
    private readonly ITreasuryDbContext _ctx;

    public GetConsolidatedStatementsHandler(ITreasuryDbContext ctx) => _ctx = ctx;

    public async Task<IReadOnlyList<ConsolidatedStatementDto>> Handle(GetConsolidatedStatementsQuery request, CancellationToken ct)
    {
        return await _ctx.ConsolidatedFinancialStatements
            .Where(s => s.ConsolidationGroupId == request.GroupId)
            .AsNoTracking()
            .Select(s => new ConsolidatedStatementDto(
                s.Id, s.FiscalYear, s.StatementType,
                s.TotalRevenue, s.TotalExpenses, s.NetIncome,
                s.TotalAssets, s.TotalLiabilities, s.TotalEquity,
                s.PreparedDate, s.Status))
            .ToListAsync(ct);
    }
}

public class ConsolidateGroupHandler : IRequestHandler<ConsolidateGroupCommand, object>
{
    private readonly ITreasuryDbContext _ctx;

    public ConsolidateGroupHandler(ITreasuryDbContext ctx) => _ctx = ctx;

    public async Task<object> Handle(ConsolidateGroupCommand request, CancellationToken ct)
    {
        var group = await _ctx.ConsolidationGroups.FindAsync(new object[] { request.GroupId }, ct);
        if (group == null) throw new KeyNotFoundException("Group not found");
        return new { status = "Consolidated", timestamp = DateTime.UtcNow };
    }
}

public class GetIntercompanyTransactionsHandler : IRequestHandler<GetIntercompanyTransactionsQuery, IReadOnlyList<IntercompanyTransactionDto>>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GetIntercompanyTransactionsHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<IntercompanyTransactionDto>> Handle(GetIntercompanyTransactionsQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        return await _ctx.IntercompanyTransactions
            .Where(t => t.ParentCompanyId == tenantId)
            .AsNoTracking()
            .Select(t => new IntercompanyTransactionDto(
                t.Id, t.ParentCompanyId, t.SubsidiaryId,
                t.Type, t.Amount, t.Currency,
                t.TransactionDate, t.Status, t.IsEliminated, t.RelatedInvoiceId))
            .ToListAsync(ct);
    }
}

public class EliminateIntercompanyHandler : IRequestHandler<EliminateIntercompanyCommand, object>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public EliminateIntercompanyHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<object> Handle(EliminateIntercompanyCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var count = await _ctx.IntercompanyTransactions
            .Where(t => t.ParentCompanyId == tenantId && !t.IsEliminated)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.IsEliminated, true)
                .SetProperty(t => t.Status, "Eliminated"), ct);
        return new { eliminated = count, message = "Intercompany transactions eliminated" };
    }
}
