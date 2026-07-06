using Erp.Application.Common.Interfaces;
using Erp.Application.DTOs;
using Erp.Application.Features.Auth.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Application.Features.Auth.Commands;

public record SwitchCompanyCommand(Guid UserId, Guid CompanyId) : IRequest<LoginResponseDto>;

public class SwitchCompanyHandler : IRequestHandler<SwitchCompanyCommand, LoginResponseDto>
{
    private readonly IApplicationDbContext _ctx;
    private readonly IJwtProvider _jwtProvider;
    private readonly IRequestHandler<GetUserCompaniesQuery, IReadOnlyList<CompanyMembershipDto>> _getUserCompanies;

    public SwitchCompanyHandler(
        IApplicationDbContext ctx,
        IJwtProvider jwtProvider,
        IRequestHandler<GetUserCompaniesQuery, IReadOnlyList<CompanyMembershipDto>> getUserCompanies)
    {
        _ctx = ctx;
        _jwtProvider = jwtProvider;
        _getUserCompanies = getUserCompanies;
    }

    public async Task<LoginResponseDto> Handle(SwitchCompanyCommand request, CancellationToken ct)
    {
        var user = await _ctx.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == request.UserId && u.IsActive, ct)
            ?? throw new UnauthorizedAccessException("Usuario no encontrado.");

        var hasAccess = await _ctx.UserCompanies.IgnoreQueryFilters()
            .AnyAsync(uc => uc.UserId == request.UserId && uc.CompanyId == request.CompanyId, ct);

        if (!hasAccess && user.CompanyId != request.CompanyId)
            throw new UnauthorizedAccessException("No tienes acceso a esa empresa.");

        var company = await _ctx.Companies.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == request.CompanyId && c.IsActive, ct)
            ?? throw new InvalidOperationException("Empresa no encontrada o inactiva.");

        var companies = await _getUserCompanies.Handle(new GetUserCompaniesQuery(request.UserId), ct);

        return new LoginResponseDto
        {
            Token = _jwtProvider.Generate(user, request.CompanyId),
            UserId = user.Id,
            Email = user.Email,
            CompanyId = company.Id.ToString(),
            CompanyName = company.Name,
            Companies = companies.ToList()
        };
    }
}
