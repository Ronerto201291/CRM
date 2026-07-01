using Erp.Application.Common.Attributes;
using Erp.Modules.Inventory.Application.Features.Inventory.Commands;
using Erp.Modules.Inventory.Application.Features.Inventory.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Inventory.API.Controllers;

[ApiController, Route("api/inventory/[controller]"), Authorize, RequiredModule("Inventory")]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;
    public ProductsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search, [FromQuery] bool? active, [FromQuery] string? type, CancellationToken ct)
        => Ok(await _mediator.Send(new GetProductsQuery { Search = search, Active = active, Type = type }, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProductByIdQuery { Id = id }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductCommand cmd, CancellationToken ct)
    {
        var result = await _mediator.Send(cmd, ct);
        return Created($"/api/inventory/products/{result.Id}", result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductCommand cmd, CancellationToken ct)
    {
        cmd.Id = id;
        var ok = await _mediator.Send(cmd, ct);
        return ok ? Ok(new { message = "Producto actualizado." }) : NotFound();
    }

    [HttpPatch("{id:guid}/activate")]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] bool active, CancellationToken ct)
    {
        var ok = await _mediator.Send(new SetProductActiveCommand { Id = id, IsActive = active }, ct);
        return ok ? Ok(new { Id = id, IsActive = active }) : NotFound();
    }

    [HttpGet("{id:guid}/movements")]
    public async Task<IActionResult> GetMovements(
        Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await _mediator.Send(new GetProductMovementsQuery { ProductId = id, Page = page, PageSize = pageSize }, ct));
}
