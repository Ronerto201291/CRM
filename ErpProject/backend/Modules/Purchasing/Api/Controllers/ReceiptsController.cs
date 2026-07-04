using Erp.Application.Common.Attributes;
using Erp.Modules.Purchasing.Application.Features.Receipts.Commands;
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

    [HttpPost]
    [RequirePermission(Permissions.Receipt.Create)]
    public async Task<IActionResult> Create([FromBody] CreateGoodsReceiptCommand cmd)
    {
        var id = await _mediator.Send(cmd);
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    [HttpGet("{id}")]
    [RequirePermission(Permissions.Receipt.Read)]
    public IActionResult Get(Guid id) => Ok(new { id });
}
