using Erp.Application.Common;
using Erp.Application.Common.Attributes;
using Erp.Modules.Billing.Application.Features.Billing.Commands;
using Erp.Modules.Billing.Application.Features.Billing.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Billing.Api.Controllers;

[ApiController, Route("api/[controller]"), Authorize, RequiredModule("Billing")]
public class InvoicesController : ControllerBase
{
    private readonly IMediator _mediator;

    public InvoicesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [RequirePermission(Permissions.Invoice.Read)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetInvoicesQuery
        {
            Status = status,
            Page = page,
            PageSize = pageSize,
        }, ct);
        Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
        return Ok(result);
    }

    [HttpPost]
    [RequirePermission(Permissions.Invoice.Create)]
    public async Task<IActionResult> Create([FromBody] CreateInvoiceCommand cmd, CancellationToken ct)
    {
        try
        {
            return Created("", await _mediator.Send(cmd, ct));
        }
        catch (PlanLimitExceededException ex)
        {
            return StatusCode(402, new { error = ex.Check.Reason, current = ex.Check.Current, max = ex.Check.Max });
        }
    }

    [HttpPost("{id}/lock")]
    [RequirePermission(Permissions.Invoice.Lock)]
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

    /// <summary>Encola anulación VERI*FACTU en AEAT (factura previamente enviada).</summary>
    [HttpPost("{id}/verifactu/anular")]
    [RequirePermission(Permissions.Invoice.Manage)]
    public async Task<IActionResult> AnulVerifactu(Guid id, CancellationToken ct)
    {
        try
        {
            var ok = await _mediator.Send(new AnulVerifactuInvoiceCommand { InvoiceId = id }, ct);
            return ok
                ? Ok(new { message = "Anulación VeriFactu encolada." })
                : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id}/verifactu/submissions")]
    [RequirePermission(Permissions.Invoice.Read)]
    public async Task<IActionResult> GetVerifactuSubmissions(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetVerifactuSubmissionsQuery(id), ct));

    /// <summary>
    /// Marca la factura como cobrada y genera el asiento de cobro (572 Banco / 430 Clientes).
    /// POST /api/invoices/{id}/pay
    /// Body (opcional): { "paymentMethod": "bank" | "cash" | "card" | "transfer" }
    /// </summary>
    [HttpPost("{id}/pay")]
    [RequirePermission(Permissions.Invoice.Manage)]
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
    [RequirePermission(Permissions.Invoice.Read)]
    public async Task<IActionResult> VerifyHashChain(
        [FromQuery] string series, [FromQuery] int fiscalYear, CancellationToken ct)
        => Ok(await _mediator.Send(new VerifyHashChainQuery { Series = series, FiscalYear = fiscalYear }, ct));

    /// <summary>
    /// Genera y descarga el PDF legal de una factura.
    /// La factura debe estar bloqueada (IsLocked = true) para cumplir la Ley 11/2021 Antifraude.
    /// GET /api/invoices/{id}/pdf
    /// </summary>
    [HttpGet("{id}/pdf")]
    [RequirePermission(Permissions.Invoice.Export)]
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
    [RequirePermission(Permissions.Invoice.Manage)]
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
    [RequirePermission(Permissions.Invoice.Export)]
    [Produces("application/xml")]
    [ProducesResponseType(typeof(FileResult), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 404)]
    public async Task<IActionResult> DownloadFacturaE(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new GenerateFacturaEQuery(id), ct);
            return File(result.XmlBytes, "application/xml", result.FileName);
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
