using Erp.Application.Common.Attributes;
using Erp.Modules.Inventory.Application.Features.Inventory.Commands;
using Erp.Modules.Inventory.Application.Features.Inventory.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Inventory.Api.Controllers;

[ApiController, Route("api/inventory/[controller]"), Authorize, RequiredModule("Inventory")]
public class WarehousesController : ControllerBase
{
    private readonly IMediator _mediator;
    public WarehousesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [RequirePermission(Permissions.Warehouse.Read)]
    public async Task<IActionResult> GetAll([FromQuery] bool? active, CancellationToken ct)
        => Ok(await _mediator.Send(new GetWarehousesQuery { Active = active }, ct));

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.Warehouse.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetWarehouseByIdQuery { Id = id }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [RequirePermission(Permissions.Warehouse.Create)]
    public async Task<IActionResult> Create([FromBody] CreateWarehouseCommand cmd, CancellationToken ct)
    {
        var result = await _mediator.Send(cmd, ct);
        return Created($"/api/inventory/warehouses/{result.Id}", result);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.Warehouse.Update)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWarehouseCommand cmd, CancellationToken ct)
    {
        cmd.Id = id;
        var ok = await _mediator.Send(cmd, ct);
        return ok ? Ok(new { message = "Almacen actualizado." }) : NotFound();
    }

    [HttpPatch("{id:guid}/activate")]
    [RequirePermission(Permissions.Warehouse.Manage)]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] bool active, CancellationToken ct)
    {
        var ok = await _mediator.Send(new SetWarehouseActiveCommand { Id = id, IsActive = active }, ct);
        return ok ? Ok(new { Id = id, IsActive = active }) : NotFound();
    }
}
