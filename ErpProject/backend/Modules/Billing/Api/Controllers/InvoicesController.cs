using Erp.Application.Common.Interfaces;
using Erp.Modules.Billing.Application.Features.Billing.Commands;
using Erp.Modules.Billing.Application.Features.Billing.Queries;
using Erp.Modules.Billing.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Billing.Api.Controllers;

[ApiController, Route("api/[controller]"), Authorize]
public class InvoicesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPlanLimitService _planLimits;
    private readonly ITenantContext _tenantContext;
    private readonly IBillingDbContext _billingCtx;
    private readonly IFacturaEService _facturaE;

    public InvoicesController(
        IMediator mediator,
        IPlanLimitService planLimits,
        ITenantContext tenantContext,
        IBillingDbContext billingCtx,
        IFacturaEService facturaE)
    {
        _mediator = mediator;
        _planLimits = planLimits;
        _tenantContext = tenantContext;
        _billingCtx = billingCtx;
        _facturaE = facturaE;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status)
        => Ok(await _mediator.Send(new GetInvoicesQuery { Status = status }));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInvoiceCommand cmd, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthCount = await _billingCtx.Invoices.CountAsync(i => i.IssueDate >= monthStart, ct);
        var check = await _planLimits.CheckInvoiceLimitAsync(tenantId, monthCount, ct);
        if (!check.Allowed)
            return StatusCode(402, new { error = check.Reason, current = check.Current, max = check.Max });

        return Created("", await _mediator.Send(cmd, ct));
    }

    [HttpPost("{id}/lock")]
    public async Task<IActionResult> Lock(Guid id, CancellationToken ct)
    {
        try
        {
            var ok = await _mediator.Send(new LockInvoiceCommand { Id = id }, ct);
            return ok
                ? Ok(new { message = "Factura bloqueada. Asiento contable generado automaticamente." })
                : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Marca la factura como cobrada y genera el asiento de cobro (572 Banco / 430 Clientes).
    /// POST /api/invoices/{id}/pay
    /// Body (opcional): { "paymentMethod": "bank" | "cash" | "card" | "transfer" }
    /// </summary>
    [HttpPost("{id}/pay")]
    public async Task<IActionResult> Pay(Guid id, [FromBody] PayInvoiceRequest? body, CancellationToken ct)
    {
        var ok = await _mediator.Send(new MarkPaidCommand
        {
            Id            = id,
            PaymentMethod = body?.PaymentMethod ?? "bank"
        }, ct);
        return ok
            ? Ok(new { message = "Factura marcada como cobrada. Asiento de cobro generado automáticamente." })
            : NotFound();
    }

    [HttpGet("verify-chain")]
    public async Task<IActionResult> VerifyHashChain(
        [FromQuery] string series, [FromQuery] int fiscalYear, CancellationToken ct)
        => Ok(await _mediator.Send(new VerifyHashChainQuery { Series = series, FiscalYear = fiscalYear }, ct));

    /// <summary>
    /// Genera y descarga el PDF legal de una factura.
    /// La factura debe estar bloqueada (IsLocked = true) para cumplir la Ley 11/2021 Antifraude.
    /// GET /api/invoices/{id}/pdf
    /// </summary>
    [HttpGet("{id}/pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileResult), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 404)]
    public async Task<IActionResult> DownloadPdf(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new GetInvoicePdfQuery(id), ct);
            return File(result.PdfBytes, "application/pdf", result.FileName);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>
    /// Envía la factura al cliente por email con el PDF adjunto.
    /// La factura debe estar bloqueada (IsLocked = true).
    /// POST /api/invoices/{id}/send
    /// </summary>
    [HttpPost("{id}/send")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 404)]
    public async Task<IActionResult> SendByEmail(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new SendInvoiceEmailCommand(id), ct);
            return Ok(new
            {
                message = $"Factura {result.InvoiceNumber} enviada correctamente a {result.RecipientEmail}.",
                recipient = result.RecipientEmail,
                invoiceNumber = result.InvoiceNumber
            });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>
    /// Genera y descarga el XML FacturaE 3.2.2 de una factura.
    /// Requerido por Ley 18/2022 Crea y Crece para facturación electrónica B2B.
    /// La factura debe estar bloqueada (IsLocked = true).
    /// GET /api/invoices/{id}/facturae
    /// </summary>
    [HttpGet("{id}/facturae")]
    [Produces("application/xml")]
    [ProducesResponseType(typeof(FileResult), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 404)]
    public async Task<IActionResult> DownloadFacturaE(Guid id, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");
        try
        {
            var (bytes, fileName) = await _facturaE.GenerateAsync(id, tenantId, ct);
            return File(bytes, "application/xml", fileName);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}

/// <summary>Optional request body for POST /api/invoices/{id}/pay</summary>
public sealed class PayInvoiceRequest
{
    /// <summary>"bank" | "cash" | "card" | "transfer"</summary>
    public string PaymentMethod { get; set; } = "bank";
}
