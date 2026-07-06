using Erp.Application.Common.Attributes;
using Erp.Modules.Accounting.Application.Commands;
using Erp.Modules.Accounting.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Accounting.Api.Controllers;

[ApiController]
[Route("api/v1/accounting/provisions")]
[Authorize]
[RequiredModule("Accounting")]
public class ProvisionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProvisionsController(IMediator mediator) => _mediator = mediator;

    /// <summary>GET /api/v1/accounting/provisions?status=Active</summary>
    [HttpGet]
    [RequirePermission(Permissions.Provision.Read)]
    public async Task<IActionResult> GetAll([FromQuery] string? status, CancellationToken ct)
        => Ok(await _mediator.Send(new GetProvisionsQuery(status), ct));

    /// <summary>GET /api/v1/accounting/provisions/{id}</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.Provision.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var prov = await _mediator.Send(new GetProvisionQuery(id), ct);
        return prov is null ? NotFound() : Ok(prov);
    }

    /// <summary>
    /// POST /api/v1/accounting/provisions
    /// Registra una provisión y genera el asiento de dotación automáticamente
    /// si las cuentas contables existen (cuentas 6xx y 4xx/14x del PGC).
    /// </summary>
    [HttpPost]
    [RequirePermission(Permissions.Provision.Create)]
    public async Task<IActionResult> Create([FromBody] CreateProvisionRequest req, CancellationToken ct)
    {
        var id = await _mediator.Send(new CreateProvisionCommand(
            req.Code, req.Description, req.Amount, req.DueDate, req.Notes), ct);

        return Created($"api/v1/accounting/provisions/{id}", new { id });
    }

    /// <summary>PUT /api/v1/accounting/provisions/{id}</summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.Provision.Update)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProvisionRequest req, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new UpdateProvisionCommand(id, req.Description, req.Amount, req.DueDate), ct);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>
    /// POST /api/v1/accounting/provisions/{id}/release
    /// Libera la provisión (Status → Released) y genera el asiento inverso
    /// cargando en 795 Exceso de provisiones según PGC 2007.
    /// </summary>
    [HttpPost("{id:guid}/release")]
    [RequirePermission(Permissions.Provision.Manage)]
    public async Task<IActionResult> Release(Guid id, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new ReleaseProvisionCommand(id), ct);
            return Ok(new { message = "Provisión liberada. Asiento contable generado." });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>DELETE /api/v1/accounting/provisions/{id} — solo si no tiene asiento</summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.Provision.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new DeleteProvisionCommand(id), ct);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}

public record CreateProvisionRequest(
    string Code,
    string Description,
    decimal Amount,
    DateTime DueDate,
    string? Notes
);

public record UpdateProvisionRequest(
    string Description,
    decimal Amount,
    DateTime DueDate
);
