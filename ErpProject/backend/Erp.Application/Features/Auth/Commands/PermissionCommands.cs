using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Application.Features.Auth.Commands;

// ─── Queries ─────────────────────────────────────────────────────────────────

/// <summary>Returns the list of effective permission strings for the current user.</summary>
public record GetMyPermissionsQuery : IRequest<IReadOnlyList<string>>;

public class GetMyPermissionsHandler : IRequestHandler<GetMyPermissionsQuery, IReadOnlyList<string>>
{
    private readonly IPermissionService _permissions;

    public GetMyPermissionsHandler(IPermissionService permissions)
    {
        _permissions = permissions;
    }

    public Task<IReadOnlyList<string>> Handle(GetMyPermissionsQuery request, CancellationToken ct)
        => _permissions.GetUserPermissionsAsync(ct);
}

/// <summary>Returns effective permissions for a specific user (Admin only).</summary>
public record GetUserPermissionsQuery(Guid UserId) : IRequest<IReadOnlyList<string>>;

public class GetUserPermissionsHandler : IRequestHandler<GetUserPermissionsQuery, IReadOnlyList<string>>
{
    private readonly IPermissionService _permissions;

    public GetUserPermissionsHandler(IPermissionService permissions)
    {
        _permissions = permissions;
    }

    public Task<IReadOnlyList<string>> Handle(GetUserPermissionsQuery request, CancellationToken ct)
        => _permissions.GetEffectivePermissionsForUserAsync(request.UserId, ct);
}

// ─── Permission DTOs ──────────────────────────────────────────────────────────

public record PermissionDto(Guid Id, string Resource, string Action, string Description);

// ─── List all permissions ─────────────────────────────────────────────────────

public record ListPermissionsQuery : IRequest<IReadOnlyList<PermissionDto>>;

public class ListPermissionsHandler : IRequestHandler<ListPermissionsQuery, IReadOnlyList<PermissionDto>>
{
    private readonly IApplicationDbContext _ctx;

    public ListPermissionsHandler(IApplicationDbContext ctx)
    {
        _ctx = ctx;
    }

    public async Task<IReadOnlyList<PermissionDto>> Handle(ListPermissionsQuery request, CancellationToken ct)
    {
        return await _ctx.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Resource)
            .ThenBy(p => p.Action)
            .Select(p => new PermissionDto(p.Id, p.Resource, p.Action, p.Description))
            .ToListAsync(ct);
    }
}

// ─── Grant permission to user ─────────────────────────────────────────────────

public record GrantPermissionCommand(
    Guid TargetUserId,
    Guid PermissionId,
    DateTime? ExpiresAt = null) : IRequest<bool>;

public class GrantPermissionHandler : IRequestHandler<GrantPermissionCommand, bool>
{
    private readonly IApplicationDbContext _ctx;
    private readonly IHttpContextCurrentUserAccessor _currentUser;

    public GrantPermissionHandler(IApplicationDbContext ctx, IHttpContextCurrentUserAccessor currentUser)
    {
        _ctx = ctx;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(GrantPermissionCommand request, CancellationToken ct)
    {
        // Remove any existing grant/deny for this user+permission pair
        var existing = await _ctx.UserPermissions
            .Where(up => up.UserId == request.TargetUserId && up.PermissionId == request.PermissionId)
            .ToListAsync(ct);

        _ctx.UserPermissions.RemoveRange(existing);

        _ctx.UserPermissions.Add(new UserPermission
        {
            Id = Guid.NewGuid(),
            UserId = request.TargetUserId,
            PermissionId = request.PermissionId,
            IsGranted = true,
            GrantedBy = _currentUser.UserId?.ToString(),
            ExpiresAt = request.ExpiresAt
        });

        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}

// ─── Revoke permission from user ─────────────────────────────────────────────

public record RevokePermissionCommand(Guid TargetUserId, Guid PermissionId) : IRequest<bool>;

public class RevokePermissionHandler : IRequestHandler<RevokePermissionCommand, bool>
{
    private readonly IApplicationDbContext _ctx;

    public RevokePermissionHandler(IApplicationDbContext ctx)
    {
        _ctx = ctx;
    }

    public async Task<bool> Handle(RevokePermissionCommand request, CancellationToken ct)
    {
        var existing = await _ctx.UserPermissions
            .Where(up => up.UserId == request.TargetUserId && up.PermissionId == request.PermissionId)
            .ToListAsync(ct);

        _ctx.UserPermissions.RemoveRange(existing);
        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}

// ─── Explicit deny (ABAC override) ───────────────────────────────────────────

public record DenyPermissionCommand(
    Guid TargetUserId,
    Guid PermissionId,
    DateTime? ExpiresAt = null) : IRequest<bool>;

public class DenyPermissionHandler : IRequestHandler<DenyPermissionCommand, bool>
{
    private readonly IApplicationDbContext _ctx;
    private readonly IHttpContextCurrentUserAccessor _currentUser;

    public DenyPermissionHandler(IApplicationDbContext ctx, IHttpContextCurrentUserAccessor currentUser)
    {
        _ctx = ctx;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(DenyPermissionCommand request, CancellationToken ct)
    {
        var existing = await _ctx.UserPermissions
            .Where(up => up.UserId == request.TargetUserId && up.PermissionId == request.PermissionId)
            .ToListAsync(ct);

        _ctx.UserPermissions.RemoveRange(existing);

        _ctx.UserPermissions.Add(new UserPermission
        {
            Id = Guid.NewGuid(),
            UserId = request.TargetUserId,
            PermissionId = request.PermissionId,
            IsGranted = false,  // explicit deny
            GrantedBy = _currentUser.UserId?.ToString(),
            ExpiresAt = request.ExpiresAt
        });

        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}
