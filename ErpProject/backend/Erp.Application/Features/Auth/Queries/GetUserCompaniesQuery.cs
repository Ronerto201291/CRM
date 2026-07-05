using Erp.Application.Common.Interfaces;
using Erp.Application.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Application.Features.Auth.Queries;

public record GetUserCompaniesQuery(Guid UserId) : IRequest<IReadOnlyList<CompanyMembershipDto>>;

public class GetUserCompaniesHandler : IRequestHandler<GetUserCompaniesQuery, IReadOnlyList<CompanyMembershipDto>>
{
    private readonly IApplicationDbContext _ctx;

    public GetUserCompaniesHandler(IApplicationDbContext ctx) => _ctx = ctx;

    public async Task<IReadOnlyList<CompanyMembershipDto>> Handle(GetUserCompaniesQuery request, CancellationToken ct)
    {
        var memberships = await _ctx.UserCompanies
            .IgnoreQueryFilters()
            .Where(uc => uc.UserId == request.UserId)
            .Include(uc => uc.Company)
            .Include(uc => uc.Role)
            .OrderByDescending(uc => uc.IsDefault)
            .ThenBy(uc => uc.Company!.Name)
            .Select(uc => new CompanyMembershipDto
            {
                CompanyId = uc.CompanyId.ToString(),
                CompanyName = uc.Company!.Name,
                IsDefault = uc.IsDefault,
                RoleId = uc.RoleId.HasValue ? uc.RoleId.Value.ToString() : null,
                RoleName = uc.Role != null ? uc.Role.Name : null,
            })
            .ToListAsync(ct);

        if (memberships.Count > 0)
            return memberships;

        var user = await _ctx.Users.IgnoreQueryFilters()
            .Include(u => u.Company)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, ct);

        if (user?.Company == null)
            return [];

        return
        [
            new CompanyMembershipDto
            {
                CompanyId = user.CompanyId.ToString(),
                CompanyName = user.Company.Name,
                IsDefault = true
            }
        ];
    }
}
