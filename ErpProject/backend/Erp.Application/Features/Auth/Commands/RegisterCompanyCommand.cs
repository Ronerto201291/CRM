using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Core;
using Erp.Domain.Entities.Licensing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Application.Features.Auth.Commands;

/// <summary>
/// Registro autónomo de nueva empresa + usuario administrador + plan Free.
/// Devuelve JWT para auto-login inmediato.
/// </summary>
public class RegisterCompanyCommand : IRequest<RegisterCompanyResponse>
{
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyTaxId { get; set; } = string.Empty;
    public string CompanyAddress { get; set; } = string.Empty;
    public string AdminEmail { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
    public string AdminFirstName { get; set; } = string.Empty;
    public string AdminLastName { get; set; } = string.Empty;
}

public class RegisterCompanyResponse
{
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}

public class RegisterCompanyHandler : IRequestHandler<RegisterCompanyCommand, RegisterCompanyResponse>
{
    private readonly IApplicationDbContext _ctx;
    private readonly IJwtProvider _jwtProvider;
    private readonly IMediator _mediator;

    public RegisterCompanyHandler(IApplicationDbContext ctx, IJwtProvider jwtProvider, IMediator mediator)
    {
        _ctx = ctx;
        _jwtProvider = jwtProvider;
        _mediator = mediator;
    }

    public async Task<RegisterCompanyResponse> Handle(RegisterCompanyCommand req, CancellationToken ct)
    {
        // Validar unicidad de CIF/NIF
        var taxIdExists = await _ctx.Companies.IgnoreQueryFilters()
            .AnyAsync(c => c.TaxId == req.CompanyTaxId, ct);
        if (taxIdExists) throw new InvalidOperationException("Ya existe una empresa con ese CIF/NIF.");

        // Validar unicidad de email
        var emailExists = await _ctx.Users.IgnoreQueryFilters()
            .AnyAsync(u => u.Email == req.AdminEmail, ct);
        if (emailExists)
            throw new InvalidOperationException("El email ya está registrado. Usa «Añadir empresa» si ya tienes cuenta.");

        // Crear suscripción Free
        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            PlanName = "Free",
            ExpirationDate = DateTime.UtcNow.AddYears(100),
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

        _ctx.Companies.Add(company);
        _ctx.Subscriptions.Add(subscription);

        // Crear roles por defecto
        var adminRole = new Role { Id = Guid.NewGuid(), CompanyId = company.Id, Name = "Admin" };
        _ctx.Roles.Add(adminRole);
        _ctx.Roles.Add(new Role { Id = Guid.NewGuid(), CompanyId = company.Id, Name = "Manager" });
        _ctx.Roles.Add(new Role { Id = Guid.NewGuid(), CompanyId = company.Id, Name = "Contable" });

        // Crear usuario administrador
        var user = new User
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            Email = req.AdminEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.AdminPassword, workFactor: 12),
            FirstName = req.AdminFirstName,
            LastName = req.AdminLastName,
            RoleId = adminRole.Id,
            IsActive = true
        };
        _ctx.Users.Add(user);

        _ctx.UserCompanies.Add(new UserCompany
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CompanyId = company.Id,
            RoleId = adminRole.Id,
            IsDefault = true
        });

        await _ctx.SaveChangesAsync(ct);

        // Send email confirmation (fire-and-forget: registration succeeds even if email fails)
        _ = Task.Run(() => _mediator.Send(new SendEmailConfirmationCommand(user.Id), CancellationToken.None));

        return new RegisterCompanyResponse
        {
            CompanyId = company.Id,
            UserId = user.Id,
            Email = user.Email,
            CompanyName = company.Name,
            Token = _jwtProvider.Generate(user)
        };
    }
}
