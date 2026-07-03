using Erp.Application.DTOs;
using Erp.Application.Features.Auth.Commands;
using Erp.Application.Features.Auth.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Erp.Application.Common.Interfaces;

namespace Erp.Application.Features.Auth.Handlers;

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IJwtProvider _jwtProvider;
    private readonly IMediator _mediator;

    public LoginCommandHandler(IApplicationDbContext context, IJwtProvider jwtProvider, IMediator mediator)
    {
        _context = context;
        _jwtProvider = jwtProvider;
        _mediator = mediator;
    }

    public async Task<LoginResponseDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // 1. Find user by email
        // Disabling query filters because login happens BEFORE tenant is resolved usually, 
        // or tenant is resolved via email matching.
        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive, cancellationToken);

        if (user == null)
            throw new UnauthorizedAccessException("Credenciales inválidas.");

        // 2. Verify Password using BCrypt only — no plaintext fallback.
        bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);

        if (!isPasswordValid)
            throw new UnauthorizedAccessException("Credenciales inválidas.");

        // 3. If 2FA is enabled, do NOT issue the JWT yet — client must complete second factor.
        if (user.TwoFactorEnabled)
        {
            return new LoginResponseDto
            {
                UserId = user.Id,
                Email = user.Email,
                CompanyId = user.CompanyId.ToString(),
                RequiresTwoFactor = true
            };
        }

        // 4. No 2FA — issue JWT directly.
        var company = await _context.Companies.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == user.CompanyId, cancellationToken);
        var companies = await _mediator.Send(new GetUserCompaniesQuery(user.Id), cancellationToken);

        return new LoginResponseDto
        {
            Token = _jwtProvider.Generate(user),
            UserId = user.Id,
            Email = user.Email,
            CompanyId = user.CompanyId.ToString(),
            CompanyName = company?.Name ?? string.Empty,
            Companies = companies.ToList()
        };
    }
}
