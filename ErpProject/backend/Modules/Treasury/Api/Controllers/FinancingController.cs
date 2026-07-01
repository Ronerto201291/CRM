using Erp.Application.Common.Interfaces;
using Erp.Modules.Treasury.Application.Interfaces;
using Erp.Modules.Treasury.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Treasury.Api.Controllers;

[ApiController]
[Route("api/v1/treasury/financing")]
[Authorize]
public class FinancingController : ControllerBase
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenantContext;

    public FinancingController(ITreasuryDbContext ctx, ITenantContext tenantContext)
    {
        _ctx = ctx;
        _tenantContext = tenantContext;
    }

    // ─── Confirming ──────────────────────────────────────────────────────────────

    [HttpGet("confirming")]
    public async Task<IActionResult> GetConfirming([FromQuery] string? status, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");
        var query = _ctx.ConfirmingOperations.Where(c => c.CompanyId == tenantId);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(c => c.Status == status);
        var ops = await query.AsNoTracking().ToListAsync(ct);
        return Ok(ops.Select(o => new
        {
            o.Id,
            o.SupplierId,
            o.InvoiceId,
            o.InvoiceAmount,
            o.AdvancePercentage,
            o.AdvanceAmount,
            o.Fee,
            o.CreationDate,
            o.DueDate,
            o.PaymentDate,
            o.Status,
            o.FinancingProvider
        }));
    }

    [HttpPost("confirming")]
    public async Task<IActionResult> CreateConfirming([FromBody] CreateConfirmingDto dto, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");
        var op = new ConfirmingOperation
        {
            Id = Guid.NewGuid(),
            CompanyId = tenantId,
            SupplierId = dto.SupplierId,
            InvoiceId = dto.InvoiceId,
            InvoiceAmount = dto.InvoiceAmount,
            AdvancePercentage = dto.AdvancePercentage,
            AdvanceAmount = dto.InvoiceAmount * dto.AdvancePercentage / 100m,
            Fee = dto.Fee,
            CreationDate = DateTime.UtcNow,
            DueDate = dto.DueDate,
            Status = "Active",
            FinancingProvider = dto.FinancingProvider,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _ctx.ConfirmingOperations.Add(op);
        await _ctx.SaveChangesAsync(ct);
        return Created("", new { id = op.Id, status = op.Status });
    }

    [HttpPatch("confirming/{id:guid}/pay")]
    public async Task<IActionResult> PayConfirming(Guid id, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");
        var op = await _ctx.ConfirmingOperations
            .FirstOrDefaultAsync(o => o.Id == id && o.CompanyId == tenantId, ct);
        if (op == null) return NotFound();
        op.Status = "Paid";
        op.PaymentDate = DateTime.UtcNow;
        op.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        return Ok(new { id = op.Id, status = op.Status, paymentDate = op.PaymentDate });
    }

    // ─── Factoring ───────────────────────────────────────────────────────────────

    [HttpGet("factoring")]
    public async Task<IActionResult> GetFactoring([FromQuery] string? status, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");
        var query = _ctx.FactoringOperations.Where(f => f.CompanyId == tenantId);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(f => f.Status == status);
        var ops = await query.AsNoTracking().ToListAsync(ct);
        return Ok(ops.Select(o => new
        {
            o.Id,
            o.ClientId,
            o.InvoiceId,
            o.InvoiceAmount,
            o.AdvancePercentage,
            o.AdvanceAmount,
            o.DiscountFee,
            o.CommissionAmount,
            o.CreationDate,
            o.DueDate,
            o.PaymentDate,
            o.Status,
            o.FactoringProvider,
            o.IsWithRecourse
        }));
    }

    [HttpPost("factoring")]
    public async Task<IActionResult> CreateFactoring([FromBody] CreateFactoringDto dto, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");
        var op = new FactoringOperation
        {
            Id = Guid.NewGuid(),
            CompanyId = tenantId,
            ClientId = dto.ClientId,
            InvoiceId = dto.InvoiceId,
            InvoiceAmount = dto.InvoiceAmount,
            AdvancePercentage = dto.AdvancePercentage,
            AdvanceAmount = dto.InvoiceAmount * dto.AdvancePercentage / 100m,
            DiscountFee = dto.DiscountFee,
            CommissionAmount = dto.CommissionAmount,
            CreationDate = DateTime.UtcNow,
            DueDate = dto.DueDate,
            Status = "Active",
            FactoringProvider = dto.FactoringProvider,
            IsWithRecourse = dto.IsWithRecourse,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _ctx.FactoringOperations.Add(op);
        await _ctx.SaveChangesAsync(ct);
        return Created("", new { id = op.Id, status = op.Status });
    }

    [HttpPatch("factoring/{id:guid}/pay")]
    public async Task<IActionResult> PayFactoring(Guid id, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");
        var op = await _ctx.FactoringOperations
            .FirstOrDefaultAsync(f => f.Id == id && f.CompanyId == tenantId, ct);
        if (op == null) return NotFound();
        op.Status = "Paid";
        op.PaymentDate = DateTime.UtcNow;
        op.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        return Ok(new { id = op.Id, status = op.Status });
    }

    // ─── Credit Lines ───────────────────────────────────────────────────────────

    [HttpGet("credit-lines")]
    public async Task<IActionResult> GetCreditLines(CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");
        var lines = await _ctx.FinancingAccounts
            .Where(f => f.CompanyId == tenantId)
            .AsNoTracking()
            .ToListAsync(ct);
        return Ok(lines.Select(l => new
        {
            l.Id,
            l.Type,
            l.Limit,
            l.UtilizedAmount,
            l.InterestRate,
            l.Status,
            l.StartDate,
            l.ExpiryDate,
            l.Provider
        }));
    }

    [HttpPost("credit-lines")]
    public async Task<IActionResult> CreateCreditLine([FromBody] CreateCreditLineDto dto, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");
        var line = new FinancingAccount
        {
            Id = Guid.NewGuid(),
            CompanyId = tenantId,
            Type = dto.Type,
            Limit = dto.Limit,
            UtilizedAmount = 0,
            InterestRate = dto.InterestRate,
            Status = "Active",
            StartDate = dto.StartDate,
            ExpiryDate = dto.ExpiryDate,
            Provider = dto.Provider,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _ctx.FinancingAccounts.Add(line);
        await _ctx.SaveChangesAsync(ct);
        return Created("", new { id = line.Id });
    }
}

public record CreateConfirmingDto(
    Guid SupplierId, Guid InvoiceId, decimal InvoiceAmount,
    decimal AdvancePercentage, decimal Fee, DateTime DueDate, string FinancingProvider);

public record CreateFactoringDto(
    Guid ClientId, Guid InvoiceId, decimal InvoiceAmount,
    decimal AdvancePercentage, decimal DiscountFee, decimal CommissionAmount,
    DateTime DueDate, string FactoringProvider, bool IsWithRecourse);

public record CreateCreditLineDto(
    string Type, decimal Limit, decimal InterestRate,
    DateTime StartDate, DateTime ExpiryDate, string Provider);