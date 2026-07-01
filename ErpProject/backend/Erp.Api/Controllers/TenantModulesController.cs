using Erp.Application.Features.TenantModules.Commands;
using Erp.Application.Features.TenantModules.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers;

[ApiController, Route("api/tenant/modules"), Authorize]
public class TenantModulesController : ControllerBase
{
    private readonly IMediator _mediator;
    public TenantModulesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _mediator.Send(new GetTenantModulesQuery(), ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTenantModuleCommand cmd, CancellationToken ct)
    {
        cmd.Id = id;
        var ok = await _mediator.Send(cmd, ct);
        return ok
            ? Ok(new { message = $"Módulo {(cmd.IsEnabled ? "habilitado" : "deshabilitado")}." })
            : NotFound();
    }
}
