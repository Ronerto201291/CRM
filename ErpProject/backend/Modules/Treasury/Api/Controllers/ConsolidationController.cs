using Erp.Application.Common.Interfaces;
using Erp.Modules.Treasury.Application.Interfaces;
using Erp.Modules.Treasury.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Treasury.Api.Controllers;

[ApiController]
[Route("api/v1/treasury/consolidation")]
[Authorize]
public class ConsolidationController : ControllerBase
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenantContext;

    public ConsolidationController(ITreasuryDbContext ctx, ITenantContext tenantContext)
    {
        _ctx = ctx;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");
        var groups = await _ctx.ConsolidationGroups
            .Where(g => g.ParentCompanyId == tenantId)
            .AsNoTracking()
            .ToListAsync(ct);
        return Ok(groups.Select(g => new
        {
            g.Id,
            g.Name,
            g.Code,
            g.ParentCompanyId,
            g.ConsolidationPercentage,
            g.ConsolidationDate,
            g.Method,
            g.Status
        }));
    }

    [HttpPost]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupDto dto, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");
        var group = new ConsolidationGroup
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Code = dto.Code,
            ParentCompanyId = tenantId,
            ConsolidationPercentage = dto.ConsolidationPercentage,
            ConsolidationDate = DateTime.UtcNow,
            Method = dto.Method,
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _ctx.ConsolidationGroups.Add(group);
        await _ctx.SaveChangesAsync(ct);
        return Created("", new { id = group.Id, status = group.Status });
    }

    [HttpGet("{groupId:guid}/subsidiaries")]
    public async Task<IActionResult> GetSubsidiaries(Guid groupId, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");
        var subs = await _ctx.SubsidiaryCompanies
            .Where(s => s.ParentCompanyId == tenantId)
            .AsNoTracking()
            .ToListAsync(ct);
        return Ok(subs.Select(s => new
        {
            s.Id,
            s.CompanyId,
            s.ParentCompanyId,
            s.OwnershipPercentage,
            s.VotingPercentage,
            s.ConsolidationMethod,
            s.AcquisitionDate,
            s.AcquisitionPrice,
            s.Status,
            s.DisposalDate
        }));
    }

    [HttpPost("{groupId:guid}/subsidiaries")]
    public async Task<IActionResult> AddSubsidiary(Guid groupId, [FromBody] AddSubsidiaryDto dto, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");
        var sub = new SubsidiaryCompany
        {
            Id = Guid.NewGuid(),
            CompanyId = dto.CompanyId,
            ParentCompanyId = tenantId,
            OwnershipPercentage = dto.OwnershipPercentage,
            VotingPercentage = dto.VotingPercentage,
            ConsolidationMethod = dto.ConsolidationMethod,
            AcquisitionDate = dto.AcquisitionDate,
            AcquisitionPrice = dto.AcquisitionPrice,
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _ctx.SubsidiaryCompanies.Add(sub);
        await _ctx.SaveChangesAsync(ct);
        return Created("", new { id = sub.Id });
    }

    [HttpGet("{groupId:guid}/financial-statements")]
    public async Task<IActionResult> GetFinancialStatements(Guid groupId, CancellationToken ct)
    {
        var statements = await _ctx.ConsolidatedFinancialStatements
            .Where(s => s.ConsolidationGroupId == groupId)
            .AsNoTracking()
            .ToListAsync(ct);
        return Ok(statements.Select(s => new
        {
            s.Id,
            s.FiscalYear,
            s.StatementType,
            s.TotalRevenue,
            s.TotalExpenses,
            s.NetIncome,
            s.TotalAssets,
            s.TotalLiabilities,
            s.TotalEquity,
            s.PreparedDate,
            s.Status
        }));
    }

    [HttpPost("{groupId:guid}/consolidate")]
    public async Task<IActionResult> ConsolidateGroup(Guid groupId, CancellationToken ct)
    {
        var group = await _ctx.ConsolidationGroups.FindAsync(new object[] { groupId }, ct)
            ?? (object?)null;
        if (group == null) return NotFound();
        return Ok(new { status = "Consolidated", timestamp = DateTime.UtcNow });
    }

    [HttpGet("{groupId:guid}/intercompany-transactions")]
    public async Task<IActionResult> GetIntercompanyTransactions(Guid groupId, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");
        var transactions = await _ctx.IntercompanyTransactions
            .Where(t => t.ParentCompanyId == tenantId)
            .AsNoTracking()
            .ToListAsync(ct);
        return Ok(transactions.Select(t => new
        {
            t.Id,
            t.ParentCompanyId,
            t.SubsidiaryId,
            t.Type,
            t.Amount,
            t.Currency,
            t.TransactionDate,
            t.Status,
            t.IsEliminated,
            t.RelatedInvoiceId
        }));
    }

    [HttpPost("{groupId:guid}/eliminate-intercompany")]
    public async Task<IActionResult> EliminateIntercompanyTransactions(Guid groupId, [FromBody] EliminateDto dto, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");
        var count = await _ctx.IntercompanyTransactions
            .Where(t => t.ParentCompanyId == tenantId && !t.IsEliminated)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.IsEliminated, true)
                .SetProperty(t => t.Status, "Eliminated"), ct);
        return Ok(new { eliminated = count, message = "Intercompany transactions eliminated" });
    }
}

public record CreateGroupDto(string Name, string Code, decimal ConsolidationPercentage, string Method);
public record AddSubsidiaryDto(
    Guid CompanyId, decimal OwnershipPercentage, decimal VotingPercentage,
    string ConsolidationMethod, DateTime AcquisitionDate, decimal AcquisitionPrice);
public record EliminateDto(Guid GroupId);