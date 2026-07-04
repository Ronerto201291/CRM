using Erp.Application.Common.Attributes;
using Erp.Modules.Accounting.Application.Commands;
using Erp.Modules.Accounting.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Accounting.Api.Controllers;

/// <summary>
/// Periodificación contable — gastos e ingresos diferidos (PGC cuentas 480/485).
///
/// GET    /api/deferred-entries           → lista (filtrable por status)
/// GET    /api/deferred-entries/{id}      → detalle
/// POST   /api/deferred-entries           → registrar nueva periodificación
/// POST   /api/deferred-entries/{id}/recognize → reconocimiento manual de un mes
/// </summary>
[ApiController]
[Route("api/deferred-entries")]
[Authorize]
[RequiredModule("Accounting")]
public class DeferredEntriesController : ControllerBase
{
    private readonly IMediator _mediator;

    public DeferredEntriesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [RequirePermission(Permissions.DeferredEntry.Read)]
    public async Task<IActionResult> GetAll([FromQuery] string? status, CancellationToken ct)
        => Ok(await _mediator.Send(new GetDeferredEntriesQuery(status), ct));

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.DeferredEntry.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _mediator.Send(new GetDeferredEntryQuery(id), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>
    /// Registra una nueva periodificación.
    /// EntryType: "PrepaidExpense" (480) | "DeferredRevenue" (485)
    /// </summary>
    [HttpPost]
    [RequirePermission(Permissions.DeferredEntry.Create)]
    public async Task<IActionResult> Create([FromBody] CreateDeferredEntryRequest req, CancellationToken ct)
    {
        try
        {
            var id = await _mediator.Send(new CreateDeferredEntryCommand(
                req.EntryType, req.Description, req.TotalAmount,
                req.PeriodStart, req.PeriodEnd,
                req.DeferralAccountCode, req.CounterpartAccountCode,
                req.SourceType, req.SourceId
            ), ct);
            return CreatedAtAction(nameof(GetById), new { id }, new { id });
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>
    /// Reconoce manualmente la cuota de un mes concreto.
    /// Útil para procesar meses pasados o forzar el reconocimiento antes del job.
    /// </summary>
    [HttpPost("{id:guid}/recognize")]
    [RequirePermission(Permissions.DeferredEntry.Manage)]
    public async Task<IActionResult> Recognize(
        Guid id, [FromBody] RecognizeMonthRequest req, CancellationToken ct)
    {
        try
        {
            var journalId = await _mediator.Send(
                new RecognizeDeferredEntryMonthCommand(id, req.Year, req.Month), ct);
            return journalId.HasValue
                ? Ok(new { journalEntryId = journalId })
                : Ok(new { message = "No se generó asiento (ya reconocido, completado o fuera del rango)." });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────
public record CreateDeferredEntryRequest(
    string EntryType,
    string Description,
    decimal TotalAmount,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    string DeferralAccountCode,
    string CounterpartAccountCode,
    string? SourceType = null,
    Guid? SourceId = null
);

public record RecognizeMonthRequest(int Year, int Month);
