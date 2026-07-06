using Erp.Application.Common.Attributes;
using Erp.Modules.Sales.Application.Features.Orders;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;

namespace Erp.Modules.Sales.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/sales/orders")]
[ApiVersion("1.0")]
[Authorize]
[RequiredModule("Sales")]
public class SalesOrdersController : ControllerBase
{
    private readonly IMediator _mediator;

    public SalesOrdersController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [RequirePermission(Permissions.SalesOrder.Read)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _mediator.Send(new GetSalesOrdersQuery(), ct));

    [HttpGet("{id}")]
    [RequirePermission(Permissions.SalesOrder.Read)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSalesOrderByIdQuery(id), ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [RequirePermission(Permissions.SalesOrder.Create)]
    public async Task<IActionResult> Create([FromBody] CreateSoDto dto, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateSalesOrderCommand(
            dto.Number,
            dto.OrderDate,
            dto.ClientId,
            dto.ClientName,
            dto.Lines.Select(l => new SalesOrderLineDto(l.ProductId, l.Quantity, l.UnitPrice)).ToList()), ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }
}

public class CreateSoDto
{
    public string Number { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public Guid? ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public List<CreateSoLineDto> Lines { get; set; } = new();
}

public class CreateSoLineDto
{
    public Guid? ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
