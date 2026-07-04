using Erp.Application.Common.Attributes;
using Erp.Modules.Purchasing.Application.Features.Orders;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;

namespace Erp.Modules.Purchasing.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/purchasing/orders")]
[ApiVersion("1.0")]
[Authorize]
[RequiredModule("Purchasing")]
public class PurchaseOrdersController : ControllerBase
{
    private readonly IMediator _mediator;

    public PurchaseOrdersController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [RequirePermission(Permissions.PurchaseOrder.Read)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _mediator.Send(new GetPurchaseOrdersQuery(), ct));

    [HttpGet("{id}")]
    [RequirePermission(Permissions.PurchaseOrder.Read)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPurchaseOrderByIdQuery(id), ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [RequirePermission(Permissions.PurchaseOrder.Create)]
    public async Task<IActionResult> Create([FromBody] CreatePoDto dto, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreatePurchaseOrderCommand(
            dto.Number,
            dto.OrderDate,
            dto.Lines.Select(l => new PurchaseOrderLineDto(l.ProductId, l.Quantity, l.UnitPrice)).ToList()), ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    [RequirePermission(Permissions.PurchaseOrder.Update)]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreatePoDto dto, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdatePurchaseOrderCommand(
            id,
            dto.Number,
            dto.OrderDate,
            dto.Lines.Select(l => new PurchaseOrderLineDto(l.ProductId, l.Quantity, l.UnitPrice)).ToList()), ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id}")]
    [RequirePermission(Permissions.PurchaseOrder.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await _mediator.Send(new DeletePurchaseOrderCommand(id), ct) ? NoContent() : NotFound();
}

public class CreatePoDto
{
    public string Number { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public List<CreatePoLineDto> Lines { get; set; } = new();
}

public class CreatePoLineDto
{
    public Guid? ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
