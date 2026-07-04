using Erp.Application.Common.Attributes;
using Erp.Modules.Crm.Application.Features.Services.Commands;
using Erp.Modules.Crm.Application.Features.Services.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Crm.Api.Controllers;

[ApiController]
[Route("api/service-catalog")]
[Authorize]
[RequiredModule("CRM")]
public class ServiceCatalogController : ControllerBase
{
    private readonly IMediator _mediator;
    public ServiceCatalogController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [RequirePermission(Permissions.ServiceCatalog.Read)]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => Ok(await _mediator.Send(new GetServiceCatalogQuery { IncludeInactive = includeInactive }, ct));

    [HttpPost]
    [RequirePermission(Permissions.ServiceCatalog.Create)]
    public async Task<IActionResult> Create([FromBody] CreateServiceCatalogItemCommand command, CancellationToken ct)
        => Created("", await _mediator.Send(command, ct));

    [HttpPut("{id}")]
    [RequirePermission(Permissions.ServiceCatalog.Update)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateServiceCatalogItemCommand command, CancellationToken ct)
    {
        command.Id = id;
        return Ok(await _mediator.Send(command, ct));
    }
}
