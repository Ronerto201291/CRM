using Erp.Application.Common.Interfaces;
using Erp.Application.Features.Auth.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Erp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ModuleRequired")]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IApplicationDbContext _ctx;

    public AdminController(IMediator mediator, IApplicationDbContext ctx)
    {
        _mediator = mediator;
        _ctx = ctx;
    }

    private string? GetCurrentUserEmail() =>
        User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
        ?? User.FindFirst("email")?.Value;

    private bool IsSuperAdmin() => GetCurrentUserEmail() == "admin@devcorp.com";

    /// <summary>Lists all tenant companies. Super-admin only.</summary>
    [HttpGet("companies")]
    public async Task<IActionResult> GetCompanies(CancellationToken ct)
    {
        if (!IsSuperAdmin()) return Forbid();

        var companies = await _ctx.Companies
            .IgnoreQueryFilters()
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.TaxId, c.IsActive, c.Country, c.CreatedAt, c.SubscriptionId })
            .ToListAsync(ct);

        var subIds = companies.Select(c => c.SubscriptionId).ToList();
        var subs = await _ctx.Subscriptions
            .Where(s => subIds.Contains(s.Id))
            .Select(s => new { s.Id, s.PlanName, s.IsActive })
            .ToListAsync(ct);

        var subMap = subs.ToDictionary(s => s.Id);

        var result = companies.Select(c => {
            subMap.TryGetValue(c.SubscriptionId, out var sub);
            return new {
                c.Id, c.Name, c.TaxId, c.IsActive, c.Country, c.CreatedAt,
                planName = sub?.PlanName ?? "—",
                subscriptionActive = sub?.IsActive ?? false
            };
        });

        return Ok(result);
    }

    /// <summary>Lists all tenant invitations. Super-admin only.</summary>
    [HttpGet("invitations")]
    public async Task<IActionResult> GetInvitations(CancellationToken ct)
    {
        if (!IsSuperAdmin()) return Forbid();

        var invitations = await _ctx.TenantInvitations
            .IgnoreQueryFilters()
            .Include(i => i.Company)
            .OrderByDescending(i => i.ExpiresAt)
            .Select(i => new {
                i.Id, i.Email, i.Token, i.IsUsed, i.ExpiresAt, i.CompanyId,
                companyName = i.Company != null ? i.Company.Name : "N/A"
            })
            .ToListAsync(ct);

        return Ok(invitations);
    }

    /// <summary>
    /// Generates a secure invite link to create a new tenant SaaS environment.
    /// Restricted to root super-admins only.
    /// </summary>
    [HttpPost("invite")]
    public async Task<IActionResult> InviteCompany([FromBody] InviteCompanyCommand command)
    {
        if (!IsSuperAdmin())
            return Forbid("Acceso denegado. Solamente el administrador raíz de Orbital puede emitir nuevas invitaciones.");

        try
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
