using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Accounting.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Erp.Modules.Accounting.Application.Features.Onboarding;

public record OnboardingSectorDto(string Id, string Name, string Description, IReadOnlyList<string> ExtraAccounts);

public record GetOnboardingSectorsQuery : IRequest<IReadOnlyList<OnboardingSectorDto>>;

public record ApplyOnboardingSectorCommand(string SectorId) : IRequest<int>;

public sealed class GetOnboardingSectorsHandler : IRequestHandler<GetOnboardingSectorsQuery, IReadOnlyList<OnboardingSectorDto>>
{
    public Task<IReadOnlyList<OnboardingSectorDto>> Handle(GetOnboardingSectorsQuery request, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<OnboardingSectorDto>>(OnboardingSectorTemplates.All);
}

/// <summary>Amplía el PGC base con cuentas sectoriales (#41).</summary>
public sealed class ApplyOnboardingSectorHandler : IRequestHandler<ApplyOnboardingSectorCommand, int>
{
    private readonly IAccountingDbContext _ctx;
    private readonly ITenantContext _tenant;
    private readonly ILogger<ApplyOnboardingSectorHandler> _logger;

    public ApplyOnboardingSectorHandler(
        IAccountingDbContext ctx, ITenantContext tenant, ILogger<ApplyOnboardingSectorHandler> logger)
    {
        _ctx = ctx;
        _tenant = tenant;
        _logger = logger;
    }

    public async Task<int> Handle(ApplyOnboardingSectorCommand request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var sector = OnboardingSectorTemplates.All.FirstOrDefault(s => s.Id == request.SectorId)
            ?? throw new InvalidOperationException($"Sector «{request.SectorId}» no reconocido.");

        var existingCodes = await _ctx.Accounts
            .Where(a => a.CompanyId == companyId)
            .Select(a => a.Code)
            .ToListAsync(ct);

        var added = 0;
        foreach (var (code, name, type) in OnboardingSectorTemplates.GetAccountsForSector(sector.Id))
        {
            if (existingCodes.Contains(code)) continue;
            _ctx.Accounts.Add(new Account
            {
                Id = Guid.NewGuid(), CompanyId = companyId, Code = code, Name = name, Type = type
            });
            added++;
        }

        if (added > 0)
            await _ctx.SaveChangesAsync(ct);

        _logger.LogInformation("Onboarding sector {Sector}: {Count} cuentas añadidas para {CompanyId}.",
            sector.Id, added, companyId);
        return added;
    }
}

internal static class OnboardingSectorTemplates
{
    public static readonly IReadOnlyList<OnboardingSectorDto> All =
    [
        new("comercio", "Comercio minorista", "Tiendas, e-commerce, distribución", ["610", "608"]),
        new("servicios", "Servicios profesionales", "Consultoría, IT, asesoría", ["623", "629"]),
        new("construccion", "Construcción", "Obra, reformas, instalaciones", ["601", "622", "211"]),
        new("hosteleria", "Hostelería", "Restauración, bares, catering", ["600", "628", "640"]),
        new("industria", "Industria / fabricación", "Producción y manufactura", ["601", "350", "680"]),
    ];

    public static IEnumerable<(string Code, string Name, string Type)> GetAccountsForSector(string sectorId) =>
        sectorId switch
        {
            "comercio" =>
            [
                ("608", "Devoluciones de compras", "Expense"),
                ("610", "Variación de existencias", "Expense"),
            ],
            "servicios" =>
            [
                ("623", "Servicios de profesionales independientes", "Expense"),
            ],
            "construccion" =>
            [
                ("601", "Compras de materias primas", "Expense"),
                ("622", "Reparaciones y conservación", "Expense"),
            ],
            "hosteleria" =>
            [
                ("608", "Devoluciones de compras", "Expense"),
            ],
            "industria" =>
            [
                ("601", "Compras de materias primas", "Expense"),
                ("680", "Amortización del inmovilizado intangible", "Expense"),
            ],
            _ => Array.Empty<(string, string, string)>(),
        };
}
