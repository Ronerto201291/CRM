using Erp.Application.DTOs;
using MediatR;

namespace Erp.Application.Features.Auth.Commands;

public class LoginCommand : IRequest<LoginResponseDto>
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
