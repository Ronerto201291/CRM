using Erp.Application.Features.AuditLogs.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers;

[ApiController, Route("api/[controller]"), Authorize]
public class AuditLogsController : ControllerBase
{
    private readonly IMediator _mediator;
    public AuditLogsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? action,
        [FromQuery] string? entityType,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetAuditLogsQuery
        {
            Action = action, EntityType = entityType,
            DateFrom = dateFrom, DateTo = dateTo,
            Page = page, PageSize = pageSize
        }, ct);
        return Ok(result);
    }
}
