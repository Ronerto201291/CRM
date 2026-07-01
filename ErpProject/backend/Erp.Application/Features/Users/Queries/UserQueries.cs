using MediatR;

namespace Erp.Application.Features.Users.Queries;

public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? RoleId { get; set; }
    public string? RoleName { get; set; }
}

public class RoleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class GetUsersQuery : IRequest<List<UserDto>>
{
}

public class GetRolesQuery : IRequest<List<RoleDto>>
{
}
