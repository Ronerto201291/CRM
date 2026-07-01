using Erp.Application.Common.Interfaces;
using Erp.Modules.Treasury.Application.Interfaces;
using Erp.Modules.Treasury.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Treasury.Api.Controllers;

[ApiController]
[Route("api/v1/treasury/guarantees")]
[Authorize]
public class GuaranteesController : ControllerBase
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenantContext;

    public GuaranteesController(ITreasuryDbContext ctx, ITenantContext tenantContext)
    {
        _ctx = ctx;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");
        var query = _ctx.Guarantees.Where(g => g.CompanyId == tenantId);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(g => g.Status == status);
        var guarantees = await query.AsNoTracking().ToListAsync(ct);
        return Ok(guarantees.Select(g => new
        {
            g.Id,
            g.Type,
            g.ReferenceNumber,
            g.Amount,
            g.CurrencyCode,
            g.RelatedEntity,
            g.Description,
            g.IssueDate,
            g.ExpiryDate,
            g.Status,
            g.ClaimedAmount,
            g.ClaimDate
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGuaranteeDto dto, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");
        var guarantee = new Guarantee
        {
            Id = Guid.NewGuid(),
            CompanyId = tenantId,
            Type = dto.Type,
            ReferenceNumber = dto.ReferenceNumber,
            Amount = dto.Amount,
            CurrencyCode = dto.CurrencyCode,
            RelatedEntity = dto.RelatedEntity,
            Description = dto.Description,
            IssueDate = dto.IssueDate,
            ExpiryDate = dto.ExpiryDate,
            Status = "Active",
            ClaimedAmount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _ctx.Guarantees.Add(guarantee);
        await _ctx.SaveChangesAsync(ct);
        return Created("", new { id = guarantee.Id, status = guarantee.Status });
    }

    [HttpPatch("{id:guid}/claim")]
    public async Task<IActionResult> ClaimGuarantee(Guid id, [FromBody] ClaimGuaranteeDto dto, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");
        var g = await _ctx.Guarantees
            .FirstOrDefaultAsync(g => g.Id == id && g.CompanyId == tenantId, ct);
        if (g == null) return NotFound();
        g.Status = "Claimed";
        g.ClaimedAmount = dto.Amount;
        g.ClaimDate = DateTime.UtcNow;
        g.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        return Ok(new { id = g.Id, status = g.Status, g.ClaimedAmount });
    }

    [HttpPatch("{id:guid}/release")]
    public async Task<IActionResult> ReleaseGuarantee(Guid id, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");
        var g = await _ctx.Guarantees
            .FirstOrDefaultAsync(g => g.Id == id && g.CompanyId == tenantId, ct);
        if (g == null) return NotFound();
        g.Status = "Released";
        g.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        return Ok(new { id = g.Id, status = g.Status });
    }

    // ─── Collateral ─────────────────────────────────────────────────────────────

    [HttpGet("collateral")]
    public async Task<IActionResult> GetCollateral(CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");
        var collaterals = await _ctx.Collaterals
            .Where(c => c.CompanyId == tenantId)
            .AsNoTracking()
            .ToListAsync(ct);
        return Ok(collaterals.Select(c => new
        {
            c.Id,
            c.Type,
            c.Description,
            c.Value,
            c.ValuationDate,
            c.LinkedAccount,
            c.Status,
            c.LTVRatio
        }));
    }

    [HttpPost("collateral")]
    public async Task<IActionResult> CreateCollateral([FromBody] CreateCollateralDto dto, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");
        var collateral = new Collateral
        {
            Id = Guid.NewGuid(),
            CompanyId = tenantId,
            Type = dto.Type,
            Description = dto.Description,
            Value = dto.Value,
            ValuationDate = DateTime.UtcNow,
            LinkedAccount = dto.LinkedAccount,
            Status = "Pledged",
            LTVRatio = dto.LTVRatio,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _ctx.Collaterals.Add(collateral);
        await _ctx.SaveChangesAsync(ct);
        return Created("", new { id = collateral.Id });
    }

    // ─── Bank Guarantees ────────────────────────────────────────────────────────

    [HttpGet("bank-guarantees")]
    public async Task<IActionResult> GetBankGuarantees([FromQuery] string? status, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");
        var query = _ctx.BankGuarantees.Where(b => b.CompanyId == tenantId);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(b => b.Status == status);
        var guarantees = await query.AsNoTracking().ToListAsync(ct);
        return Ok(guarantees.Select(g => new
        {
            g.Id,
            g.GuaranteeNumber,
            g.Bank,
            g.Amount,
            g.Type,
            g.IssuedDate,
            g.ExpiryDate,
            g.BeneficiaryId,
            g.BeneficiaryName,
            g.Fee,
            g.Status
        }));
    }

    [HttpPost("bank-guarantees")]
    public async Task<IActionResult> CreateBankGuarantee([FromBody] CreateBankGuaranteeDto dto, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");
        var bg = new BankGuarantee
        {
            Id = Guid.NewGuid(),
            CompanyId = tenantId,
            GuaranteeNumber = dto.GuaranteeNumber,
            Bank = dto.Bank,
            Amount = dto.Amount,
            Type = dto.Type,
            IssuedDate = dto.IssuedDate,
            ExpiryDate = dto.ExpiryDate,
            BeneficiaryId = dto.BeneficiaryId,
            BeneficiaryName = dto.BeneficiaryName,
            Fee = dto.Fee,
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _ctx.BankGuarantees.Add(bg);
        await _ctx.SaveChangesAsync(ct);
        return Created("", new { id = bg.Id });
    }
}

public record CreateGuaranteeDto(
    string Type, string ReferenceNumber, decimal Amount, string CurrencyCode,
    string RelatedEntity, string Description, DateTime IssueDate, DateTime ExpiryDate);

public record ClaimGuaranteeDto(decimal Amount);

public record CreateCollateralDto(
    string Type, string Description, decimal Value, string LinkedAccount, decimal LTVRatio);

public record CreateBankGuaranteeDto(
    string GuaranteeNumber, string Bank, decimal Amount, string Type,
    DateTime IssuedDate, DateTime ExpiryDate, Guid? BeneficiaryId,
    string BeneficiaryName, decimal Fee);