using Erp.Application.Common.Attributes;
using Erp.Modules.Inventory.Application.Features.Inventory.Commands;
using Erp.Modules.Inventory.Application.Features.Inventory.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Inventory.Api.Controllers;

[ApiController, Route("api/inventory/[controller]"), Authorize, RequiredModule("Inventory")]
public class StockController : ControllerBase
{
    private readonly IMediator _mediator;
    public StockController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetStock(
        [FromQuery] Guid? warehouseId, [FromQuery] Guid? productId,
        [FromQuery] bool? belowReorderPoint, CancellationToken ct)
        => Ok(await _mediator.Send(new GetStockQuery
        {
            WarehouseId = warehouseId, ProductId = productId, BelowReorderPoint = belowReorderPoint
        }, ct));

    [HttpGet("{productId:guid}/warehouses")]
    public async Task<IActionResult> GetByProduct(Guid productId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetStockByProductQuery { ProductId = productId }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("valuation")]
    public async Task<IActionResult> GetValuation([FromQuery] Guid? warehouseId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetStockValuationQuery { WarehouseId = warehouseId }, ct));

    [HttpGet("/api/inventory/movements")]
    public async Task<IActionResult> GetMovements(
        [FromQuery] Guid? productId, [FromQuery] Guid? warehouseId,
        [FromQuery] string? movementType, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetStockMovementsQuery
        {
            ProductId = productId, WarehouseId = warehouseId, MovementType = movementType,
            From = from, To = to, Page = page, PageSize = pageSize
        }, ct);
        Response.Headers["X-Total-Count"] = result.Total.ToString();
        return Ok(result);
    }

    [HttpPost("adjustment")]
    public async Task<IActionResult> Adjust([FromBody] AdjustStockCommand cmd, CancellationToken ct)
        => Ok(await _mediator.Send(cmd, ct));
}
