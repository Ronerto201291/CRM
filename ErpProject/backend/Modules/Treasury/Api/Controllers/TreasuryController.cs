using System.Globalization;
using Erp.Application.Common.Interfaces;
using Erp.Modules.Treasury.Application.Interfaces;
using Erp.Modules.Treasury.Domain.Entities;
using Erp.Modules.Treasury.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Modules.Treasury.Api.Controllers;

/// <summary>
/// Gestión de Tesorería: cuentas bancarias, movimientos, conciliación automática,
/// efectos comerciales (letras), órdenes de pago y previsión de flujo de caja.
/// </summary>
[ApiController]
[Route("api/treasury")]
[Authorize]
public class TreasuryController : ControllerBase
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenantContext;

    public TreasuryController(ITreasuryDbContext ctx, ITenantContext tenantContext)
    {
        _ctx = ctx;
        _tenantContext = tenantContext;
    }

    // ─── Bank Accounts ─────────────────────────────────────────────────────────

    [HttpGet("bank-accounts")]
    public async Task<IActionResult> GetBankAccounts(CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");
        var accounts = await _ctx.BankAccounts
            .Where(b => b.CompanyId == tenantId)
            .AsNoTracking()
            .ToListAsync(ct);
        return Ok(accounts.Select(b => new
        {
            b.Id, b.Name, b.Iban, b.BIC, b.BankName,
            b.CurrentBalance, b.CurrencyCode, b.IsActive, b.Notes
        }));
    }

    [HttpGet("bank-accounts/{id:guid}")]
    public async Task<IActionResult> GetBankAccount(Guid id, CancellationToken ct)
    {
        var account = await _ctx.BankAccounts.Where(b => b.Id == id)
            .AsNoTracking().FirstOrDefaultAsync(ct);
        if (account == null) return NotFound();
        return Ok(new
        {
            account.Id, account.Name, account.Iban, account.BIC, account.BankName,
            account.CurrentBalance, account.CurrencyCode, account.IsActive, account.Notes
        });
    }

    [HttpPost("bank-accounts")]
    public async Task<IActionResult> CreateBankAccount(
        [FromBody] CreateBankAccountRequest req, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var entity = new BankAccount
        {
            Id = Guid.NewGuid(),
            CompanyId = tenantId,
            Name = req.Name,
            Iban = req.Iban.Replace(" ", "").ToUpperInvariant(),
            BIC = req.BIC,
            BankName = req.BankName,
            AccountingAccountCode = req.AccountingAccountCode ?? "572",
            CurrentBalance = 0,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _ctx.BankAccounts.Add(entity);
        await _ctx.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetBankAccount), new { id = entity.Id }, new
        {
            entity.Id, entity.Name, entity.Iban, entity.BIC, entity.BankName,
            entity.CurrentBalance, entity.CurrencyCode, entity.IsActive
        });
    }

    // ─── Bank Movements ─────────────────────────────────────────────────────────

    [HttpGet("bank-accounts/{id:guid}/movements")]
    public async Task<IActionResult> GetMovements(Guid id,
        [FromQuery] bool? unreconciled, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var q = _ctx.BankMovements.Where(m => m.BankAccountId == id);
        if (unreconciled == true) q = q.Where(m => !m.IsReconciled);
        if (from.HasValue) q = q.Where(m => m.Date >= from.Value);
        if (to.HasValue) q = q.Where(m => m.Date <= to.Value);

        var movements = await q.OrderByDescending(m => m.Date).AsNoTracking().ToListAsync(ct);
        return Ok(movements.Select(m => new
        {
            m.Id, m.BankAccountId,
            Date = m.Date.ToString("yyyy-MM-dd"),
            m.Reference, m.Description, m.Amount, m.Type,
            m.IsReconciled, m.Origin
        }));
    }

    /// <summary>
    /// POST /api/treasury/bank-accounts/{id}/import
    /// Importa extracto bancario en CSV (formato: Fecha,Importe,Concepto,Referencia).
    /// </summary>
    [HttpPost("bank-accounts/{id:guid}/import")]
    public async Task<IActionResult> ImportStatement(Guid id,
        [FromBody] ImportStatementRequest req, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var account = await _ctx.BankAccounts
            .Where(b => b.Id == id && b.CompanyId == tenantId)
            .FirstOrDefaultAsync(ct);
        if (account == null) return NotFound("Cuenta no encontrada");

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(req.CsvContent));
        using var reader = new StreamReader(stream);
        var movements = new List<BankMovement>();
        var lineNumber = 0;

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            lineNumber++;
            if (lineNumber == 1) continue;
            var parts = line.Split(',');
            if (parts.Length < 3) continue;
            if (!DateTime.TryParse(parts[0].Trim(), out var date)) continue;
            if (!decimal.TryParse(parts[1].Trim(), NumberStyles.Any,
                CultureInfo.InvariantCulture, out var amount)) continue;

            var description = parts.Length > 2 ? parts[2].Trim() : string.Empty;
            var reference = parts.Length > 3 ? parts[3].Trim() : $"IMP-{lineNumber}";

            movements.Add(new BankMovement
            {
                Id = Guid.NewGuid(),
                CompanyId = tenantId,
                BankAccountId = id,
                Date = date,
                Amount = amount,
                Type = amount >= 0 ? "Credit" : "Debit",
                Description = description,
                Reference = reference,
                Origin = "BankImport",
                OriginalBankRef = reference,
                IsReconciled = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (movements.Count > 0)
        {
            await _ctx.BankMovements.AddRangeAsync(movements, ct);
            await _ctx.SaveChangesAsync(ct);
        }

        return Ok(new { imported = movements.Count });
    }

    /// <summary>
    /// POST /api/treasury/bank-accounts/{id}/reconcile
    /// Ejecuta la conciliación bancaria automática.
    /// </summary>
    [HttpPost("bank-accounts/{id:guid}/reconcile")]
    public async Task<IActionResult> Reconcile(Guid id, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var reconciliationSvc = HttpContext.RequestServices
            .GetRequiredService<BankReconciliationService>();

        var result = await reconciliationSvc.ReconcileAsync(id, ct);
        return Ok(new { matchedCount = result.MatchedCount, matchedAmount = result.MatchedAmount });
    }

    // ─── Cash Effects ─────────────────────────────────────────────────────────

    [HttpGet("effects")]
    public async Task<IActionResult> GetEffects([FromQuery] string? status, CancellationToken ct)
    {
        var q = _ctx.CashEffects.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(e => e.Status == status);

        var effects = await q.OrderBy(e => e.DueDate).AsNoTracking().ToListAsync(ct);
        return Ok(effects.Select(e => new
        {
            e.Id, e.EffectNumber, e.ClientName, e.ClientTaxId,
            e.Amount,
            IssueDate = e.IssueDate.ToString("yyyy-MM-dd"),
            DueDate = e.DueDate.ToString("yyyy-MM-dd"),
            e.Status, e.BankAccountId
        }));
    }

    [HttpPost("effects")]
    public async Task<IActionResult> CreateEffect(
        [FromBody] CreateCashEffectRequest req, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var effect = new CashEffect
        {
            Id = Guid.NewGuid(),
            CompanyId = tenantId,
            ClientId = req.ClientId,
            ClientName = req.ClientName,
            ClientTaxId = req.ClientTaxId,
            EffectNumber = req.EffectNumber,
            IssueDate = req.IssueDate,
            DueDate = req.DueDate,
            Amount = req.Amount,
            Status = "Pending",
            BankAccountId = req.BankAccountId,
            Notes = req.Notes,
            CreatedAt = DateTime.UtcNow
        };

        _ctx.CashEffects.Add(effect);
        await _ctx.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetEffects), new { id = effect.Id }, new
        {
            effect.Id, effect.EffectNumber, effect.ClientName, effect.Amount,
            DueDate = effect.DueDate.ToString("yyyy-MM-dd"), effect.Status
        });
    }

    [HttpPatch("effects/{id:guid}/status")]
    public async Task<IActionResult> UpdateEffectStatus(
        Guid id, [FromBody] UpdateEffectStatusRequest req, CancellationToken ct)
    {
        var effect = await _ctx.CashEffects.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (effect == null) return NotFound();

        effect.Status = req.NewStatus;
        await _ctx.SaveChangesAsync(ct);

        return Ok(new { id = effect.Id, status = effect.Status });
    }

    // ─── Payment Orders ─────────────────────────────────────────────────────────

    [HttpGet("payment-orders")]
    public async Task<IActionResult> GetPaymentOrders([FromQuery] string? status, CancellationToken ct)
    {
        var q = _ctx.PaymentOrders.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(p => p.Status == status);

        var orders = await q.OrderBy(p => p.ScheduledDate).AsNoTracking().ToListAsync(ct);
        return Ok(orders.Select(p => new
        {
            p.Id, p.PaymentType, p.BeneficiaryName, p.BeneficiaryIban,
            p.Amount, p.Status,
            ScheduledDate = p.ScheduledDate?.ToString("yyyy-MM-dd"),
            ExecutedAt = p.ExecutedAt?.ToString("yyyy-MM-dd"),
            p.BankAccountId, p.Notes
        }));
    }

    [HttpPost("payment-orders")]
    public async Task<IActionResult> CreatePaymentOrder(
        [FromBody] CreatePaymentOrderRequest req, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var order = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            CompanyId = tenantId,
            PaymentType = req.PaymentType,
            BeneficiaryName = req.BeneficiaryName,
            BeneficiaryTaxId = req.BeneficiaryTaxId,
            BeneficiaryIban = req.BeneficiaryIban.Replace(" ", "").ToUpperInvariant(),
            Description = req.Description,
            Amount = req.Amount,
            ScheduledDate = req.ScheduledDate,
            BankAccountId = req.BankAccountId,
            Status = "Draft",
            CreatedAt = DateTime.UtcNow
        };

        _ctx.PaymentOrders.Add(order);
        await _ctx.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetPaymentOrders), new { id = order.Id }, new
        {
            order.Id, order.PaymentType, order.BeneficiaryName,
            order.Amount, order.Status,
            ScheduledDate = order.ScheduledDate?.ToString("yyyy-MM-dd")
        });
    }

    // ─── Cash Flow Forecast ────────────────────────────────────────────────────

    [HttpGet("forecasts")]
    public async Task<IActionResult> GetForecasts(
        [FromQuery] int? year, [FromQuery] int? month, CancellationToken ct)
    {
        var q = _ctx.CashFlowForecasts.AsQueryable();
        if (year.HasValue) q = q.Where(f => f.ForecastDate.Year == year.Value);
        if (month.HasValue) q = q.Where(f => f.ForecastDate.Month == month.Value);

        var forecasts = await q.OrderBy(f => f.ForecastDate).AsNoTracking().ToListAsync(ct);
        return Ok(forecasts.Select(f => new
        {
            f.Id,
            ForecastDate = f.ForecastDate.ToString("yyyy-MM-dd"),
            f.ExpectedInflow, f.ExpectedOutflow, f.ExpectedBalance,
            f.Source, f.SourceId, f.IsActual, f.Notes
        }));
    }
}

// ─── Request DTOs ────────────────────────────────────────────────────────────

public record CreateBankAccountRequest(
    string Name, string Iban, string? BIC, string BankName, string? AccountingAccountCode);

public record ImportStatementRequest(string CsvContent);

public record CreateCashEffectRequest(
    Guid? ClientId, string ClientName, string ClientTaxId,
    string EffectNumber, DateTime IssueDate, DateTime DueDate,
    decimal Amount, Guid? BankAccountId, string? Notes);

public record UpdateEffectStatusRequest(string NewStatus);

public record CreatePaymentOrderRequest(
    string PaymentType, string BeneficiaryName, string BeneficiaryTaxId,
    string BeneficiaryIban, string Description, decimal Amount,
    DateTime? ScheduledDate, Guid? BankAccountId);

// Dto para el resultado de conciliación
public record ReconciliationResultDto(int MatchedCount, decimal MatchedAmount, string Message);
