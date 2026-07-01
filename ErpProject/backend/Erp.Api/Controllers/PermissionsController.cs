using Erp.Application.Features.Auth.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers;

/// <summary>
/// Admin-only ABAC permission management endpoints.
/// Allows listing all permissions, viewing the current user's permissions,
/// and granting/revoking/denying permissions on a per-user basis.
/// </summary>
[ApiController]
[Route("api/permissions")]
[Authorize]
public class PermissionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PermissionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Lists all permissions defined in the system (Admin only).</summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ListPermissions(CancellationToken ct)
        => Ok(await _mediator.Send(new ListPermissionsQuery(), ct));

    /// <summary>Returns the effective permissions for the currently authenticated user.</summary>
    [HttpGet("my")]
    public async Task<IActionResult> MyPermissions(CancellationToken ct)
        => Ok(await _mediator.Send(new GetMyPermissionsQuery(), ct));

    /// <summary>Grants a permission to a specific user (Admin only).</summary>
    [HttpPost("grant")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Grant([FromBody] GrantPermissionCommand cmd, CancellationToken ct)
        => Ok(await _mediator.Send(cmd, ct));

    /// <summary>Removes a user-level permission override (grant or deny) for a specific user (Admin only).</summary>
    [HttpPost("revoke")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Revoke([FromBody] RevokePermissionCommand cmd, CancellationToken ct)
        => Ok(await _mediator.Send(cmd, ct));

    /// <summary>Creates an explicit deny override for a specific user (Admin only).</summary>
    [HttpPost("deny")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Deny([FromBody] DenyPermissionCommand cmd, CancellationToken ct)
        => Ok(await _mediator.Send(cmd, ct));
}
