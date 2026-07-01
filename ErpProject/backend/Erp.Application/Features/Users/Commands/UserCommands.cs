using MediatR;

namespace Erp.Application.Features.Users.Commands;

public class CreateUserResult
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string TempPassword { get; set; } = string.Empty;
}

public class CreateUserCommand : IRequest<CreateUserResult>
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public Guid? RoleId { get; set; }
}

public class UpdateUserRoleCommand : IRequest<bool>
{
    public Guid UserId { get; set; }
    public Guid? RoleId { get; set; }
}

public class UpdateUserStatusCommand : IRequest<bool>
{
    public Guid UserId { get; set; }
    public bool IsActive { get; set; }
}
