using Erp.Application.Common.Interfaces;
using Erp.Application.Features.Users.Commands;
using Erp.Application.Features.Users.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers;

[ApiController, Route("api/[controller]"), Authorize]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPlanLimitService _planLimits;
    private readonly ITenantContext _tenantContext;

    public UsersController(IMediator mediator, IPlanLimitService planLimits, ITenantContext tenantContext)
    {
        _mediator = mediator;
        _planLimits = planLimits;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _mediator.Send(new GetUsersQuery(), ct));

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles(CancellationToken ct)
        => Ok(await _mediator.Send(new GetRolesQuery(), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserCommand cmd, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var check = await _planLimits.CheckUserLimitAsync(tenantId, ct);
        if (!check.Allowed)
            return StatusCode(402, new { error = check.Reason, current = check.Current, max = check.Max });

        return Ok(await _mediator.Send(cmd, ct));
    }

    [HttpPatch("{id:guid}/role")]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateUserRoleCommand cmd, CancellationToken ct)
    {
        cmd.UserId = id;
        var ok = await _mediator.Send(cmd, ct);
        return ok ? Ok(new { message = "Rol actualizado correctamente." }) : NotFound();
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateUserStatusCommand cmd, CancellationToken ct)
    {
        cmd.UserId = id;
        var ok = await _mediator.Send(cmd, ct);
        return ok ? Ok(new { message = cmd.IsActive ? "Usuario activado." : "Usuario desactivado." }) : NotFound();
    }
}
