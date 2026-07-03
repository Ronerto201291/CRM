using Erp.Application.Features.Auth.Commands;
using Erp.Application.Features.Admin.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ModuleRequired")]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminController(IMediator mediator) => _mediator = mediator;

    private string? GetCurrentUserEmail() =>
        User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
        ?? User.FindFirst("email")?.Value;

    private bool IsSuperAdmin() => GetCurrentUserEmail() == "admin@devcorp.com";

    [HttpGet("companies")]
    public async Task<IActionResult> GetCompanies(CancellationToken ct)
    {
        if (!IsSuperAdmin()) return Forbid();
        return Ok(await _mediator.Send(new GetAdminCompaniesQuery(), ct));
    }

    [HttpGet("invitations")]
    public async Task<IActionResult> GetInvitations(CancellationToken ct)
    {
        if (!IsSuperAdmin()) return Forbid();
        return Ok(await _mediator.Send(new GetAdminInvitationsQuery(), ct));
    }

    [HttpPost("invite")]
    public async Task<IActionResult> InviteCompany([FromBody] InviteCompanyCommand command)
    {
        if (!IsSuperAdmin())
            return Forbid("Acceso denegado. Solamente el administrador raíz de Orbital puede emitir nuevas invitaciones.");

        try
        {
            return Ok(await _mediator.Send(command));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
