using Erp.Application.Common.Attributes;
using Erp.Modules.Treasury.Application.Features.OpenBanking;
using Erp.Modules.Treasury.Application.Features.Treasury.Commands;
using Erp.Modules.Treasury.Application.Features.Treasury.Handlers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Treasury.Api.Controllers;

/// <summary>
/// Gestión de Tesorería: cuentas bancarias, movimientos, conciliación automática,
/// efectos comerciales (letras), órdenes de pago y previsión de flujo de caja.
/// Todos los sub-recursos (efectos, órdenes de pago, previsiones) se autorizan bajo
/// el permiso BankAccount:* hasta que se justifique un recurso ABAC propio para cada uno
/// (ver ADR-0018 #42c).
/// </summary>
[ApiController]
[Route("api/treasury")]
[Authorize]
[RequiredModule("Treasury")]
public class TreasuryController : ControllerBase
{
    private readonly IMediator _mediator;

    public TreasuryController(IMediator mediator) => _mediator = mediator;

    // ─── Bank Accounts ─────────────────────────────────────────────────────────

    [HttpGet("bank-accounts")]
    [RequirePermission(Permissions.BankAccount.Read)]
    public async Task<IActionResult> GetBankAccounts(CancellationToken ct)
    {
        var accounts = await _mediator.Send(new GetBankAccountsQuery(), ct);
        return Ok(accounts.Select(b => new
        {
            b.Id, b.Name, b.Iban, b.BIC, b.BankName,
            b.CurrentBalance, b.CurrencyCode, b.IsActive, b.Notes
        }));
    }

    [HttpGet("bank-accounts/{id:guid}")]
    [RequirePermission(Permissions.BankAccount.Read)]
    public async Task<IActionResult> GetBankAccount(Guid id, CancellationToken ct)
    {
        var account = await _mediator.Send(new GetBankAccountQuery(id), ct);
        if (account == null) return NotFound();
        return Ok(new
        {
            account.Id, account.Name, account.Iban, account.BIC, account.BankName,
            account.CurrentBalance, account.CurrencyCode, account.IsActive, account.Notes
        });
    }

    [HttpPost("bank-accounts")]
    [RequirePermission(Permissions.BankAccount.Create)]
    public async Task<IActionResult> CreateBankAccount(
        [FromBody] CreateBankAccountRequest req, CancellationToken ct)
    {
        var account = await _mediator.Send(new CreateBankAccountCommand(
            req.Name, req.Iban, req.BIC, req.BankName, req.AccountingAccountCode), ct);
        return CreatedAtAction(nameof(GetBankAccount), new { id = account.Id }, new
        {
            account.Id, account.Name, account.Iban, account.BIC, account.BankName,
            account.CurrentBalance, account.CurrencyCode, account.IsActive
        });
    }

    // ─── Bank Movements ─────────────────────────────────────────────────────────

    [HttpGet("bank-accounts/{id:guid}/movements")]
    [RequirePermission(Permissions.BankAccount.Read)]
    public async Task<IActionResult> GetMovements(Guid id,
        [FromQuery] bool? unreconciled, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetBankMovementsQuery(
            id, unreconciled == true, from, to, page, pageSize), ct);
        Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
        return Ok(new
        {
            items = result.Items.Select(m => new
            {
                m.Id, m.BankAccountId,
                Date = m.Date.ToString("yyyy-MM-dd"),
                m.Reference, m.Description, m.Amount, m.Type,
                m.IsReconciled, m.Origin
            }),
            totalCount = result.TotalCount,
            page = result.Page,
            pageSize = result.PageSize
        });
    }

    [HttpPost("bank-accounts/{id:guid}/import")]
    [RequirePermission(Permissions.BankAccount.Manage)]
    public async Task<IActionResult> ImportStatement(Guid id,
        [FromBody] ImportStatementRequest req, CancellationToken ct)
    {
        var imported = await _mediator.Send(new ImportBankStatementCommand(id, req.CsvContent), ct);
        return Ok(new { imported = imported.Count });
    }

    [HttpPost("bank-accounts/{id:guid}/reconcile")]
    [RequirePermission(Permissions.BankAccount.Manage)]
    public async Task<IActionResult> Reconcile(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new ReconcileBankAccountCommand(id), ct);
        return Ok(new { matchedCount = result.MatchedCount, matchedAmount = result.MatchedAmount });
    }

    [HttpPost("bank-accounts/{id:guid}/sync-open-banking")]
    [RequirePermission(Permissions.BankAccount.Manage)]
    public async Task<IActionResult> SyncOpenBanking(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new SyncOpenBankingCommand(id), ct);
            return Ok(new
            {
                provider = result.Provider,
                imported = result.ImportedCount,
                skippedDuplicates = result.SkippedDuplicates,
                reconciledCount = result.ReconciledCount,
                reconciledAmount = result.ReconciledAmount,
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ─── Cash Effects ─────────────────────────────────────────────────────────

    [HttpGet("effects")]
    [RequirePermission(Permissions.BankAccount.Read)]
    public async Task<IActionResult> GetEffects(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCashEffectsQuery(status, page, pageSize), ct);
        Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
        return Ok(new
        {
            items = result.Items.Select(e => new
            {
                e.Id, e.EffectNumber, e.ClientName, e.ClientTaxId,
                e.Amount,
                IssueDate = e.IssueDate.ToString("yyyy-MM-dd"),
                DueDate = e.DueDate.ToString("yyyy-MM-dd"),
                e.Status, e.BankAccountId
            }),
            totalCount = result.TotalCount,
            page = result.Page,
            pageSize = result.PageSize
        });
    }

    [HttpPost("effects")]
    [RequirePermission(Permissions.BankAccount.Manage)]
    public async Task<IActionResult> CreateEffect(
        [FromBody] CreateCashEffectRequest req, CancellationToken ct)
    {
        var effect = await _mediator.Send(new CreateCashEffectCommand(
            req.ClientId, req.ClientName, req.ClientTaxId,
            req.EffectNumber, req.IssueDate, req.DueDate,
            req.Amount, req.BankAccountId, req.Notes), ct);
        return CreatedAtAction(nameof(GetEffects), new { id = effect.Id }, new
        {
            effect.Id, effect.EffectNumber, effect.ClientName, effect.Amount,
            DueDate = effect.DueDate.ToString("yyyy-MM-dd"), effect.Status
        });
    }

    [HttpPatch("effects/{id:guid}/status")]
    [RequirePermission(Permissions.BankAccount.Manage)]
    public async Task<IActionResult> UpdateEffectStatus(
        Guid id, [FromBody] UpdateEffectStatusRequest req, CancellationToken ct)
    {
        try
        {
            var effect = await _mediator.Send(new UpdateCashEffectStatusCommand(id, req.NewStatus), ct);
            return Ok(new { id = effect.Id, status = effect.Status });
        }
        catch (InvalidOperationException) { return NotFound(); }
    }

    /// <summary>Genera XML SEPA pain.001 para cobrar el efecto (cliente → empresa).</summary>
    [HttpPost("effects/{id:guid}/sepa")]
    [RequirePermission(Permissions.BankAccount.Manage)]
    public async Task<IActionResult> GenerateEffectSepa(
        Guid id, [FromBody] GenerateEffectSepaRequest req, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(
                new GenerateCashEffectSepaCommand(id, req.ClientIban, req.ClientBic), ct);
            return File(result.XmlBytes, "application/xml", result.FileName);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Descarga el último XML SEPA generado para el efecto.</summary>
    [HttpGet("effects/{id:guid}/sepa")]
    [RequirePermission(Permissions.BankAccount.Read)]
    public async Task<IActionResult> DownloadEffectSepa(Guid id, CancellationToken ct)
    {
        var effect = await _mediator.Send(new GetCashEffectByIdQuery(id), ct);
        if (effect?.SEPAXml is null)
            return NotFound(new { error = "No hay XML SEPA generado para este efecto." });

        var bytes = System.Text.Encoding.UTF8.GetBytes(effect.SEPAXml);
        var fileName = $"SEPA_Cobro_{effect.EffectNumber}.xml";
        return File(bytes, "application/xml", fileName);
    }

    /// <summary>Genera XML SEPA pain.008 (adeudo directo) para cobrar el efecto.</summary>
    [HttpPost("effects/{id:guid}/sepa/sdd")]
    [RequirePermission(Permissions.BankAccount.Manage)]
    public async Task<IActionResult> GenerateEffectSdd(
        Guid id, [FromBody] GenerateEffectSddRequest req, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(
                new GenerateCashEffectSddCommand(
                    id, req.ClientIban, req.ClientBic,
                    req.CreditorId, req.MandateId, req.MandateSignatureDate), ct);
            return File(result.XmlBytes, "application/xml", result.FileName);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Descarga el último XML SEPA SDD generado para el efecto.</summary>
    [HttpGet("effects/{id:guid}/sepa/sdd")]
    [RequirePermission(Permissions.BankAccount.Read)]
    public async Task<IActionResult> DownloadEffectSdd(Guid id, CancellationToken ct)
    {
        var effect = await _mediator.Send(new GetCashEffectByIdQuery(id), ct);
        if (effect?.SEPAXml is null || !effect.SEPAXml.Contains("CstmrDrctDbtInitn", StringComparison.Ordinal))
            return NotFound(new { error = "No hay XML SEPA SDD generado para este efecto." });

        var bytes = System.Text.Encoding.UTF8.GetBytes(effect.SEPAXml);
        var fileName = $"SEPA_SDD_{effect.EffectNumber}.xml";
        return File(bytes, "application/xml", fileName);
    }

    // ─── Payment Orders ─────────────────────────────────────────────────────────

    [HttpGet("payment-orders")]
    [RequirePermission(Permissions.BankAccount.Read)]
    public async Task<IActionResult> GetPaymentOrders(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetPaymentOrdersQuery(status, page, pageSize), ct);
        Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
        return Ok(new
        {
            items = result.Items.Select(p => new
            {
                p.Id, p.PaymentType, p.BeneficiaryName, p.BeneficiaryIban,
                p.Amount, p.Status,
                ScheduledDate = p.ScheduledDate?.ToString("yyyy-MM-dd"),
                ExecutedAt = p.ExecutedAt?.ToString("yyyy-MM-dd"),
                p.BankAccountId, p.Notes
            }),
            totalCount = result.TotalCount,
            page = result.Page,
            pageSize = result.PageSize
        });
    }

    [HttpPost("payment-orders")]
    [RequirePermission(Permissions.BankAccount.Manage)]
    public async Task<IActionResult> CreatePaymentOrder(
        [FromBody] CreatePaymentOrderRequest req, CancellationToken ct)
    {
        var order = await _mediator.Send(new CreatePaymentOrderCommand(
            req.PaymentType, req.BeneficiaryName, req.BeneficiaryTaxId,
            req.BeneficiaryIban, req.Description, req.Amount,
            req.ScheduledDate, req.BankAccountId, null, null), ct);
        return CreatedAtAction(nameof(GetPaymentOrders), new { id = order.Id }, new
        {
            order.Id, order.PaymentType, order.BeneficiaryName,
            order.Amount, order.Status,
            ScheduledDate = order.ScheduledDate?.ToString("yyyy-MM-dd")
        });
    }

    [HttpPost("payment-orders/{id:guid}/execute")]
    public async Task<IActionResult> ExecutePaymentOrder(Guid id, CancellationToken ct)
    {
        try
        {
            var order = await _mediator.Send(new ExecutePaymentOrderCommand(id), ct);
            return Ok(new
            {
                order.Id, order.PaymentType, order.BeneficiaryName,
                order.Amount, order.Status,
                ExecutedAt = order.ExecutedAt?.ToString("yyyy-MM-dd"),
                order.BankAccountId,
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ─── Cash Flow Forecast ────────────────────────────────────────────────────

    [HttpGet("forecasts")]
    [RequirePermission(Permissions.BankAccount.Read)]
    public async Task<IActionResult> GetForecasts(
        [FromQuery] int? year, [FromQuery] int? month, CancellationToken ct)
    {
        var forecasts = await _mediator.Send(new GetCashFlowForecastQuery(year, month), ct);
        return Ok(forecasts.Select(f => new
        {
            f.Id,
            ForecastDate = f.ForecastDate.ToString("yyyy-MM-dd"),
            f.ExpectedInflow, f.ExpectedOutflow, f.ExpectedBalance,
            f.Source, f.SourceId, f.IsActual, f.Notes
        }));
    }

    [HttpGet("liquidity-forecast")]
    [RequirePermission(Permissions.BankAccount.Read)]
    public async Task<IActionResult> GetLiquidityForecast(CancellationToken ct)
        => Ok(await _mediator.Send(new GetTreasuryLiquidityForecastQuery(), ct));
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

public record GenerateEffectSepaRequest(string ClientIban, string? ClientBic);

public record GenerateEffectSddRequest(
    string ClientIban,
    string? ClientBic,
    string CreditorId,
    string MandateId,
    DateTime MandateSignatureDate);

public record CreatePaymentOrderRequest(
    string PaymentType, string BeneficiaryName, string BeneficiaryTaxId,
    string BeneficiaryIban, string Description, decimal Amount,
    DateTime? ScheduledDate, Guid? BankAccountId);
