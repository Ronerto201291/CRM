using Erp.Modules.Accounting.Application.Commands;
using Erp.Modules.Accounting.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Accounting.Api.Controllers;

[ApiController]
[Route("api/v1/accounting/fixed-assets")]
[Authorize]
public class FixedAssetsController : ControllerBase
{
    private readonly IMediator _mediator;

    public FixedAssetsController(IMediator mediator) => _mediator = mediator;

    /// <summary>GET /api/v1/accounting/fixed-assets?status=Active</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status, CancellationToken ct)
        => Ok(await _mediator.Send(new GetFixedAssetsQuery(status), ct));

    /// <summary>GET /api/v1/accounting/fixed-assets/{id}</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var asset = await _mediator.Send(new GetFixedAssetQuery(id), ct);
        return asset is null ? NotFound() : Ok(asset);
    }

    /// <summary>POST /api/v1/accounting/fixed-assets — Registrar nuevo activo fijo</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFixedAssetRequest req, CancellationToken ct)
    {
        var id = await _mediator.Send(new CreateFixedAssetCommand(
            req.AssetCode,
            req.Name,
            req.Description,
            req.AcquisitionDate,
            req.CommissioningDate,
            req.AcquisitionCost,
            req.ResidualValue,
            req.UsefulLifeYears,
            req.AmortizationMethod,
            req.AssetAccountCode,
            req.DepreciationAccountCode,
            req.AccumDepreciationAccountCode,
            req.Notes), ct);

        return Created($"api/v1/accounting/fixed-assets/{id}", new { id });
    }

    /// <summary>PUT /api/v1/accounting/fixed-assets/{id} — Actualizar metadatos del activo</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateFixedAssetRequest req, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new UpdateFixedAssetCommand(
                id, req.Name, req.Description, req.ResidualValue, req.UsefulLifeYears, req.Notes), ct);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>POST /api/v1/accounting/fixed-assets/{id}/dispose — Dar de baja el activo</summary>
    [HttpPost("{id:guid}/dispose")]
    public async Task<IActionResult> Dispose(Guid id, [FromBody] DisposeFixedAssetRequest req, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new DisposeFixedAssetCommand(id, req.DisposedAt, req.Notes), ct);
            return Ok(new { message = "Activo dado de baja correctamente." });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>
    /// POST /api/v1/accounting/fixed-assets/{id}/depreciate
    /// Genera manualmente el asiento de dotación de amortización para el mes indicado.
    /// Si no se especifica mes/año, usa el mes actual.
    /// </summary>
    [HttpPost("{id:guid}/depreciate")]
    public async Task<IActionResult> Depreciate(Guid id, [FromBody] PostAmortizationRequest? req, CancellationToken ct)
    {
        var now   = DateTime.UtcNow;
        var year  = req?.Year  ?? now.Year;
        var month = req?.Month ?? now.Month;

        try
        {
            var journalEntryId = await _mediator.Send(
                new PostMonthlyAmortizationCommand(id, year, month), ct);

            if (journalEntryId is null)
                return Ok(new { message = "No hay dotación pendiente para este activo/mes (ya procesado o activo totalmente amortizado)." });

            return Ok(new { journalEntryId, message = $"Asiento de amortización generado para {month:D2}/{year}." });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record CreateFixedAssetRequest(
    string AssetCode,
    string Name,
    string? Description,
    DateTime AcquisitionDate,
    DateTime CommissioningDate,
    decimal AcquisitionCost,
    decimal ResidualValue,
    int UsefulLifeYears,
    string AmortizationMethod,        // Linear | Declining | Accelerated
    string AssetAccountCode,          // 21x
    string DepreciationAccountCode,   // 68x
    string AccumDepreciationAccountCode, // 28x
    string? Notes
);

public record UpdateFixedAssetRequest(
    string Name,
    string? Description,
    decimal ResidualValue,
    int UsefulLifeYears,
    string? Notes
);

public record DisposeFixedAssetRequest(
    DateTime DisposedAt,
    string? Notes
);

public record PostAmortizationRequest(
    int? Year,
    int? Month
);
