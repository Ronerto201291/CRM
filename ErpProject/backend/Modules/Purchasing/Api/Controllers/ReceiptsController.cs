using Erp.Application.Common.Attributes;
using Erp.Modules.Purchasing.Application.Features.Receipts.Commands;
using Erp.Modules.Purchasing.Application.Features.Receipts.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;

namespace Erp.Modules.Purchasing.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/purchasing/receipts")]
[ApiVersion("1.0")]
[Authorize]
[RequiredModule("Purchasing")]
public class ReceiptsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ReceiptsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [RequirePermission(Permissions.Receipt.Read)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetAllGoodsReceiptsQuery(page, pageSize, search), ct);
        return Ok(result);
    }

    [HttpPost]
    [RequirePermission(Permissions.Receipt.Create)]
    public async Task<IActionResult> Create([FromBody] CreateGoodsReceiptCommand cmd)
    {
        var id = await _mediator.Send(cmd);
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.Receipt.Read)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var receipt = await _mediator.Send(new GetGoodsReceiptQuery(id), ct);
        if (receipt == null) return NotFound(new { error = "Goods receipt not found" });
        return Ok(receipt);
    }
}
