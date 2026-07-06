using Erp.Application.Features.Auth.Commands;
using Erp.Application.Features.Admin.Queries;
using Erp.Application.Options;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Erp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ModuleRequired")]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly PlatformOptions _platform;

    public AdminController(IMediator mediator, IOptions<PlatformOptions> platform)
    {
        _mediator = mediator;
        _platform = platform.Value;
    }

    private string? GetCurrentUserEmail() =>
        User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
        ?? User.FindFirst("email")?.Value;

    private bool IsSuperAdmin()
    {
        var email = GetCurrentUserEmail();
        return !string.IsNullOrEmpty(email)
            && _platform.SuperAdminEmails.Contains(email, StringComparer.OrdinalIgnoreCase);
    }

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
