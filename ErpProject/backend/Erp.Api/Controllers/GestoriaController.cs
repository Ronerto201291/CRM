using Erp.Application.Features.Gestoria;
using Erp.Application.Features.Auth.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Erp.Api.Controllers;

[ApiController, Route("api/gestoria"), Authorize]
public class GestoriaController : ControllerBase
{
    private readonly IMediator _mediator;
    public GestoriaController(IMediator mediator) => _mediator = mediator;

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        return Ok(await _mediator.Send(new GetGestoriaDashboardQuery(userId.Value), ct));
    }

    [HttpGet("memberships")]
    public async Task<IActionResult> GetMemberships(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var companies = await _mediator.Send(new GetUserCompaniesQuery(userId.Value), ct);
        return Ok(companies);
    }

    [HttpPut("memberships/{companyId:guid}/role")]
    public async Task<IActionResult> UpdateRole(Guid companyId, [FromBody] UpdateRoleRequest body, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!Guid.TryParse(body.RoleId, out var roleId))
            return BadRequest(new { error = "RoleId inválido." });

        try
        {
            var ok = await _mediator.Send(new UpdateUserCompanyRoleCommand(userId.Value, companyId, roleId), ct);
            return ok ? Ok(new { message = "Rol actualizado." }) : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private Guid? GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}

public record UpdateRoleRequest(string RoleId);
