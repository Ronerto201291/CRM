using Erp.Modules.Treasury.Application.Features.Treasury.Commands;
using Erp.Modules.Treasury.Application.Features.Treasury.Handlers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    private readonly IMediator _mediator;

    public TreasuryController(IMediator mediator) => _mediator = mediator;

    // ─── Bank Accounts ─────────────────────────────────────────────────────────

    [HttpGet("bank-accounts")]
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
    public async Task<IActionResult> ImportStatement(Guid id,
        [FromBody] ImportStatementRequest req, CancellationToken ct)
    {
        var imported = await _mediator.Send(new ImportBankStatementCommand(id, req.CsvContent), ct);
        return Ok(new { imported = imported.Count });
    }

    [HttpPost("bank-accounts/{id:guid}/reconcile")]
    public async Task<IActionResult> Reconcile(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new ReconcileBankAccountCommand(id), ct);
        return Ok(new { matchedCount = result.MatchedCount, matchedAmount = result.MatchedAmount });
    }

    // ─── Cash Effects ─────────────────────────────────────────────────────────

    [HttpGet("effects")]
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

    // ─── Payment Orders ─────────────────────────────────────────────────────────

    [HttpGet("payment-orders")]
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

    // ─── Cash Flow Forecast ────────────────────────────────────────────────────

    [HttpGet("forecasts")]
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
