using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Application.Features.Auth.Commands;

public class AcceptInviteCommand : IRequest<AcceptInviteResponse>
{
    public string Token { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}

public class AcceptInviteResponse
{
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string LoginToken { get; set; } = string.Empty;
}

public class AcceptInviteHandler : IRequestHandler<AcceptInviteCommand, AcceptInviteResponse>
{
    private readonly IApplicationDbContext _ctx;
    private readonly IJwtProvider _jwtProvider;

    public AcceptInviteHandler(IApplicationDbContext ctx, IJwtProvider jwtProvider)
    {
        _ctx = ctx;
        _jwtProvider = jwtProvider;
    }

    public async Task<AcceptInviteResponse> Handle(AcceptInviteCommand req, CancellationToken ct)
    {
        // Buscar la invitación por token
        var invitation = await _ctx.TenantInvitations.IgnoreQueryFilters()
            .Include(i => i.Company)
            .FirstOrDefaultAsync(i => i.Token == req.Token, ct);

        if (invitation == null)
            throw new InvalidOperationException("Enlace de invitación no válido o no existe.");

        if (invitation.IsUsed)
            throw new InvalidOperationException("Esta invitación ya ha sido utilizada.");

        if (invitation.ExpiresAt < DateTime.UtcNow)
            throw new InvalidOperationException("La invitación ha caducado.");

        // Validar unicidad del TaxId final proporcionado por el usuario
        var taxIdExists = await _ctx.Companies.IgnoreQueryFilters()
            .AnyAsync(c => c.TaxId == req.TaxId && c.Id != invitation.CompanyId, ct);
        if (taxIdExists) 
            throw new InvalidOperationException("Ya existe otra empresa con ese CIF/NIF en el sistema.");

        // Actualizar la compañía con el verdadero TaxId
        var company = invitation.Company;
        if (company == null)
            throw new InvalidOperationException("Compañía asociada no encontrada.");

        company.TaxId = req.TaxId;

        // Buscar el rol administrador
        var adminRole = await _ctx.Roles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.CompanyId == company.Id && r.Name == "Admin", ct);

        if (adminRole == null)
            throw new InvalidOperationException("Rol de Administrador no encontrado para esta empresa.");

        // Crear el usuario administrador
        var user = new User
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            Email = invitation.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password, workFactor: 12),
            FirstName = req.FirstName,
            LastName = req.LastName,
            RoleId = adminRole.Id,
            IsActive = true
        };

        _ctx.Users.Add(user);

        // Marcar la invitación como usada
        invitation.IsUsed = true;

        await _ctx.SaveChangesAsync(ct);

        return new AcceptInviteResponse
        {
            CompanyId = company.Id,
            UserId = user.Id,
            Email = user.Email,
            CompanyName = company.Name,
            LoginToken = _jwtProvider.Generate(user)
        };
    }
}
