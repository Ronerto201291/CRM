using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Core;
using Erp.Domain.Entities.Licensing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Erp.Application.Features.Auth.Commands;

public class InviteCompanyCommand : IRequest<InviteCompanyResponse>
{
    public string CompanyName { get; set; } = string.Empty;
    public string AdminEmail { get; set; } = string.Empty;
}

public class InviteCompanyResponse
{
    public Guid CompanyId { get; set; }
    public Guid InvitationId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string InviteUrlPath { get; set; } = string.Empty;
}

public class InviteCompanyHandler : IRequestHandler<InviteCompanyCommand, InviteCompanyResponse>
{
    private readonly IApplicationDbContext _ctx;

    public InviteCompanyHandler(IApplicationDbContext ctx)
    {
        _ctx = ctx;
    }

    public async Task<InviteCompanyResponse> Handle(InviteCompanyCommand req, CancellationToken ct)
    {
        // Verificar si el email ya existe en invites pendientes o usuarios
        var emailExists = await _ctx.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == req.AdminEmail, ct);
        if (emailExists) throw new InvalidOperationException("El email ya está registrado en el sistema.");

        var pendingInvite = await _ctx.TenantInvitations.IgnoreQueryFilters()
            .AnyAsync(i => i.Email == req.AdminEmail && !i.IsUsed && i.ExpiresAt > DateTime.UtcNow, ct);
        if (pendingInvite) throw new InvalidOperationException("Ya existe una invitación pendiente para este correo.");

        // Crear Company con campos requeridos pero TaxId = PENDING
        // El cliente rellenará el CIF después
        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            PlanName = "Pro", // Assuming invited clients start on a premium plan or trial
            ExpirationDate = DateTime.UtcNow.AddMonths(1), // Trial
            ActiveModules = """["Billing","Crm","Accounting","Inventory","Expenses"]""",
            IsActive = true,
            StripeStatus = "trialing"
        };

        var company = new Erp.Domain.Entities.Core.Company
        {
            Id = Guid.NewGuid(),
            Name = req.CompanyName,
            TaxId = "PENDING-" + Guid.NewGuid().ToString("N").Substring(0, 8),
            Country = "ES",
            IsActive = true,
            SubscriptionId = subscription.Id,
            PublicUploadToken = Guid.NewGuid().ToString("N"),
            QrUploadEnabled = false
        };

        subscription.CompanyId = company.Id;

        _ctx.Companies.Add(company);
        _ctx.Subscriptions.Add(subscription);

        // Crear roles base obligatorios para la empresa
        _ctx.Roles.Add(new Role { Id = Guid.NewGuid(), CompanyId = company.Id, Name = "Admin" });
        _ctx.Roles.Add(new Role { Id = Guid.NewGuid(), CompanyId = company.Id, Name = "Manager" });
        _ctx.Roles.Add(new Role { Id = Guid.NewGuid(), CompanyId = company.Id, Name = "Contable" });

        // Generar token seguro
        var tokenBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(tokenBytes);
        }
        var secureToken = Convert.ToBase64String(tokenBytes).Replace("+", "-").Replace("/", "_").Replace("=", "");

        var invitation = new TenantInvitation
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            Email = req.AdminEmail,
            Token = secureToken,
            ExpiresAt = DateTime.UtcNow.AddDays(7), // Expira en 7 días
            IsUsed = false
        };

        _ctx.TenantInvitations.Add(invitation);

        await _ctx.SaveChangesAsync(ct);

        return new InviteCompanyResponse
        {
            CompanyId = company.Id,
            InvitationId = invitation.Id,
            Token = secureToken,
            Email = req.AdminEmail,
            InviteUrlPath = $"/register?token={secureToken}"
        };
    }
}
