using Erp.Application.Common.Interfaces;
using Erp.Application.Features.Users.Commands;
using Erp.Application.Features.Users.Queries;
using Erp.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Erp.Application.Features.Users.Handlers;

public class GetUsersHandler : IRequestHandler<GetUsersQuery, List<UserDto>>
{
    private readonly IApplicationDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GetUsersHandler(IApplicationDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx; _tenant = tenant;
    }

    public async Task<List<UserDto>> Handle(GetUsersQuery request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new UnauthorizedAccessException("No tenant context.");

        return await _ctx.Users
            .Include(u => u.Role)
            .Where(u => u.CompanyId == companyId)
            .OrderBy(u => u.FirstName)
            .Select(u => new UserDto
            {
                Id = u.Id, Email = u.Email, FirstName = u.FirstName, LastName = u.LastName,
                IsActive = u.IsActive, TwoFactorEnabled = u.TwoFactorEnabled, CreatedAt = u.CreatedAt,
                RoleId = u.RoleId, RoleName = u.Role != null ? u.Role.Name : null
            })
            .ToListAsync(ct);
    }
}

public class GetRolesHandler : IRequestHandler<GetRolesQuery, List<RoleDto>>
{
    private readonly IApplicationDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GetRolesHandler(IApplicationDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx; _tenant = tenant;
    }

    public async Task<List<RoleDto>> Handle(GetRolesQuery request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new UnauthorizedAccessException("No tenant context.");

        return await _ctx.Roles
            .Where(r => r.CompanyId == companyId)
            .Select(r => new RoleDto { Id = r.Id, Name = r.Name })
            .ToListAsync(ct);
    }
}

public class CreateUserHandler : IRequestHandler<CreateUserCommand, CreateUserResult>
{
    private readonly IApplicationDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CreateUserHandler(IApplicationDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx; _tenant = tenant;
    }

    public async Task<CreateUserResult> Handle(CreateUserCommand request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new UnauthorizedAccessException("No tenant context.");

        if (await _ctx.Users.AnyAsync(u => u.Email == request.Email && u.CompanyId == companyId, ct))
            throw new InvalidOperationException("Ya existe un usuario con ese email.");

        var tempPassword = GenerateTempPassword();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword);

        var user = new User
        {
            CompanyId    = companyId,
            Email        = request.Email.Trim().ToLowerInvariant(),
            FirstName    = request.FirstName,
            LastName     = request.LastName ?? string.Empty,
            PasswordHash = passwordHash,
            RoleId       = request.RoleId,
            IsActive     = true
        };

        _ctx.Users.Add(user);
        await _ctx.SaveChangesAsync(ct);

        return new CreateUserResult
        {
            Id = user.Id, Email = user.Email, FirstName = user.FirstName,
            LastName = user.LastName, IsActive = user.IsActive,
            CreatedAt = user.CreatedAt, TempPassword = tempPassword
        };
    }

    private static string GenerateTempPassword()
    {
        const string chars = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#$";
        var bytes = RandomNumberGenerator.GetBytes(12);
        return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
    }
}

public class UpdateUserRoleHandler : IRequestHandler<UpdateUserRoleCommand, bool>
{
    private readonly IApplicationDbContext _ctx;
    private readonly ITenantContext _tenant;

    public UpdateUserRoleHandler(IApplicationDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx; _tenant = tenant;
    }

    public async Task<bool> Handle(UpdateUserRoleCommand request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new UnauthorizedAccessException("No tenant context.");

        var user = await _ctx.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && u.CompanyId == companyId, ct);
        if (user == null) return false;

        if (request.RoleId.HasValue)
        {
            var roleExists = await _ctx.Roles.AnyAsync(r => r.Id == request.RoleId.Value && r.CompanyId == companyId, ct);
            if (!roleExists) throw new KeyNotFoundException("Rol no encontrado.");
        }

        user.RoleId = request.RoleId;
        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}

public class UpdateUserStatusHandler : IRequestHandler<UpdateUserStatusCommand, bool>
{
    private readonly IApplicationDbContext _ctx;
    private readonly ITenantContext _tenant;

    public UpdateUserStatusHandler(IApplicationDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx; _tenant = tenant;
    }

    public async Task<bool> Handle(UpdateUserStatusCommand request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new UnauthorizedAccessException("No tenant context.");

        var user = await _ctx.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && u.CompanyId == companyId, ct);
        if (user == null) return false;

        user.IsActive = request.IsActive;
        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}
