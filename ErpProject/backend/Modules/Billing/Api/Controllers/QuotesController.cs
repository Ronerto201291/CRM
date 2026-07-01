using Erp.Modules.Billing.Application.Features.Quotes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Billing.Api.Controllers;

[ApiController, Route("api/[controller]"), Authorize]
public class QuotesController : ControllerBase
{
    private readonly IMediator _mediator;

    public QuotesController(IMediator mediator) => _mediator = mediator;

    // ─── LIST ────────────────────────────────────────────────────────────────

    /// <summary>Lista de presupuestos con filtros opcionales.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] string? clientName,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken ct)
        => Ok(await _mediator.Send(new GetQuotesQuery
        {
            Status = status,
            ClientName = clientName,
            DateFrom = dateFrom,
            DateTo = dateTo
        }, ct));

    // ─── SINGLE ──────────────────────────────────────────────────────────────

    /// <summary>Obtiene un presupuesto con líneas e historial de estados.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetQuoteQuery { Id = id }, ct);
        return result is null ? NotFound() : Ok(result);
    }

    // ─── PDF ─────────────────────────────────────────────────────────────────

    /// <summary>Descarga el PDF del presupuesto.</summary>
    [HttpGet("{id:guid}/pdf")]
    [Produces("application/pdf")]
    public async Task<IActionResult> DownloadPdf(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new GetQuotePdfQuery { Id = id }, ct);
            return File(result.PdfBytes, "application/pdf", result.FileName);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    // ─── CREATE ───────────────────────────────────────────────────────────────

    /// <summary>Crea un nuevo presupuesto en estado Draft.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateQuoteCommand cmd, CancellationToken ct)
    {
        try
        {
            var id = await _mediator.Send(cmd, ct);
            return Created($"api/quotes/{id}", new { id });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ─── UPDATE ───────────────────────────────────────────────────────────────

    /// <summary>Edita un presupuesto en estado Draft (recalcula totales).</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateQuoteCommand cmd, CancellationToken ct)
    {
        cmd.Id = id;
        try
        {
            var ok = await _mediator.Send(cmd, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ─── SEND ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Envía el presupuesto al cliente por email (Draft → Sent).
    /// Genera PDF y lo adjunta. Incluye link al portal de aceptación.
    /// </summary>
    [HttpPost("{id:guid}/send")]
    public async Task<IActionResult> Send(Guid id, [FromBody] SendQuoteRequest? body, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new SendQuoteCommand
            {
                Id = id,
                AttachPdf = body?.AttachPdf ?? true
            }, ct);

            return Ok(new
            {
                message = $"Presupuesto {result.QuoteNumber} enviado a {result.SentToEmail}.",
                quoteNumber = result.QuoteNumber,
                sentTo = result.SentToEmail,
                portalUrl = result.PortalUrl
            });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ─── ACCEPT (registro manual por el comercial) ────────────────────────────

    /// <summary>Registra la aceptación manualmente (el comercial indica que el cliente aceptó).</summary>
    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, CancellationToken ct)
    {
        try
        {
            var ok = await _mediator.Send(new AcceptQuoteCommand { Id = id }, ct);
            return ok ? Ok(new { message = "Presupuesto aceptado." }) : NotFound();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ─── REJECT ───────────────────────────────────────────────────────────────

    /// <summary>Registra el rechazo del presupuesto.</summary>
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectQuoteRequest? body, CancellationToken ct)
    {
        try
        {
            var ok = await _mediator.Send(new RejectQuoteCommand
            {
                Id = id,
                Reason = body?.Reason
            }, ct);
            return ok ? Ok(new { message = "Presupuesto rechazado." }) : NotFound();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ─── CONVERT TO INVOICE ───────────────────────────────────────────────────

    /// <summary>
    /// Convierte el presupuesto aceptado en una factura (Draft).
    /// Requiere que el cliente sea de tipo Registered (con ClientId en CRM).
    /// </summary>
    [HttpPost("{id:guid}/convert")]
    public async Task<IActionResult> ConvertToInvoice(Guid id, [FromBody] ConvertQuoteRequest body, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new ConvertQuoteToInvoiceCommand
            {
                Id = id,
                InvoiceSeries = body.InvoiceSeries ?? "A",
                DueDate = body.DueDate,
                IrpfRate = body.IrpfRate
            }, ct);

            return Ok(new
            {
                message = $"Presupuesto convertido a factura {result.InvoiceNumber} (borrador).",
                invoiceId = result.InvoiceId,
                invoiceNumber = result.InvoiceNumber
            });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { error = ex.Message }); }
    }

    // ─── DUPLICATE ────────────────────────────────────────────────────────────

    /// <summary>Crea una copia del presupuesto como nuevo Draft.</summary>
    [HttpPost("{id:guid}/duplicate")]
    public async Task<IActionResult> Duplicate(Guid id, CancellationToken ct)
    {
        try
        {
            var newId = await _mediator.Send(new DuplicateQuoteCommand { Id = id }, ct);
            return Created($"api/quotes/{newId}", new { id = newId });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    // ─── NEW VERSION ──────────────────────────────────────────────────────────

    /// <summary>
    /// Crea una nueva versión del presupuesto (V2, V3...).
    /// La versión anterior pasa a estado Superseded.
    /// Solo permitido desde estados Sent/Accepted/Rejected.
    /// </summary>
    [HttpPost("{id:guid}/new-version")]
    public async Task<IActionResult> NewVersion(Guid id, CancellationToken ct)
    {
        try
        {
            var newId = await _mediator.Send(new NewQuoteVersionCommand { Id = id }, ct);
            return Created($"api/quotes/{newId}", new { id = newId });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}

// ─── Request DTOs ─────────────────────────────────────────────────────────────

public record SendQuoteRequest(bool AttachPdf = true);

public record RejectQuoteRequest(string? Reason);

public record ConvertQuoteRequest(
    string? InvoiceSeries,
    DateTime DueDate,
    decimal IrpfRate = 0);
