using Erp.Application.Common.Interfaces;
using Erp.Application.DTOs;
using Erp.Application.Features.Auth.Queries;
using Erp.Domain.Entities.Core;
using Erp.Domain.Entities.Licensing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Application.Features.Auth.Commands;

/// <summary>
/// Alta de una empresa adicional vinculada al usuario autenticado (ADR-0018 #42a fase 4-5).
/// No crea un nuevo User — añade Company + UserCompany.
/// </summary>
public class AddCompanyFromAccountCommand : IRequest<LoginResponseDto>
{
    public Guid UserId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyTaxId { get; set; } = string.Empty;
    public string CompanyAddress { get; set; } = string.Empty;
}

public class AddCompanyFromAccountHandler : IRequestHandler<AddCompanyFromAccountCommand, LoginResponseDto>
{
    private readonly IApplicationDbContext _ctx;
    private readonly IJwtProvider _jwtProvider;
    private readonly IRequestHandler<GetUserCompaniesQuery, IReadOnlyList<CompanyMembershipDto>> _getUserCompanies;

    public AddCompanyFromAccountHandler(
        IApplicationDbContext ctx,
        IJwtProvider jwtProvider,
        IRequestHandler<GetUserCompaniesQuery, IReadOnlyList<CompanyMembershipDto>> getUserCompanies)
    {
        _ctx = ctx;
        _jwtProvider = jwtProvider;
        _getUserCompanies = getUserCompanies;
    }

    public async Task<LoginResponseDto> Handle(AddCompanyFromAccountCommand req, CancellationToken ct)
    {
        var user = await _ctx.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == req.UserId && u.IsActive, ct)
            ?? throw new UnauthorizedAccessException("Usuario no encontrado.");

        var taxIdExists = await _ctx.Companies.IgnoreQueryFilters()
            .AnyAsync(c => c.TaxId == req.CompanyTaxId, ct);
        if (taxIdExists)
            throw new InvalidOperationException("Ya existe una empresa con ese CIF/NIF.");

        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            PlanName = "Free",
            ExpirationDate = DateTime.UtcNow.AddYears(100),
            ActiveModules = """["Billing","Crm"]""",
            IsActive = true,
            StripeStatus = "active"
        };

        var company = new Erp.Domain.Entities.Core.Company
        {
            Id = Guid.NewGuid(),
            Name = req.CompanyName,
            TaxId = req.CompanyTaxId,
            Address = req.CompanyAddress,
            Country = "ES",
            IsActive = true,
            SubscriptionId = subscription.Id,
            PublicUploadToken = Guid.NewGuid().ToString("N"),
            QrUploadEnabled = false
        };
        subscription.CompanyId = company.Id;

        var adminRole = new Role { Id = Guid.NewGuid(), CompanyId = company.Id, Name = "Admin" };
        _ctx.Roles.Add(adminRole);
        _ctx.Roles.Add(new Role { Id = Guid.NewGuid(), CompanyId = company.Id, Name = "Manager" });
        _ctx.Roles.Add(new Role { Id = Guid.NewGuid(), CompanyId = company.Id, Name = "Contable" });

        _ctx.Companies.Add(company);
        _ctx.Subscriptions.Add(subscription);
        _ctx.UserCompanies.Add(new UserCompany
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CompanyId = company.Id,
            RoleId = adminRole.Id,
            IsDefault = false
        });

        await _ctx.SaveChangesAsync(ct);

        var companies = await _getUserCompanies.Handle(new GetUserCompaniesQuery(user.Id), ct);

        return new LoginResponseDto
        {
            Token = _jwtProvider.Generate(user, company.Id),
            UserId = user.Id,
            Email = user.Email,
            CompanyId = company.Id.ToString(),
            CompanyName = company.Name,
            Companies = companies.ToList()
        };
    }
}
