using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Accounting.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Application.Features.Isp;

public record GetIspOperationsQuery : IRequest<IReadOnlyList<IspOperationDto>>;
public record GetIspOperationQuery(Guid Id) : IRequest<IspOperationDto?>;
public record CreateIspOperationCommand(string SupplierCountryCode, decimal VatableBase, decimal VatRate)
    : IRequest<IspOperationDto>;
public record CalculateIspVatCommand(Guid Id, decimal Amount) : IRequest<IspVatCalculationDto>;

public record IspOperationDto(
    Guid Id,
    string SupplierCountryCode,
    decimal VatableBase,
    decimal VatRate,
    decimal VatAmount,
    bool IsReverseCharge,
    string Status);

public record IspVatCalculationDto(
    Guid Id,
    decimal InvoiceVat,
    bool IspApplied,
    decimal EffectiveVat,
    string Message,
    string Status);

public sealed class GetIspOperationsHandler : IRequestHandler<GetIspOperationsQuery, IReadOnlyList<IspOperationDto>>
{
    private readonly IAccountingDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GetIspOperationsHandler(IAccountingDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<IspOperationDto>> Handle(GetIspOperationsQuery request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        var rows = await _ctx.InversionDeSujetoActivos
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    internal static IspOperationDto ToDto(InversionDeSujetoActivo x) =>
        new(x.Id, x.SupplierCountryCode, x.VatableBase, x.VatRate, x.VatAmount, x.IsReverseCharge, "Active");
}

public sealed class GetIspOperationHandler : IRequestHandler<GetIspOperationQuery, IspOperationDto?>
{
    private readonly IAccountingDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GetIspOperationHandler(IAccountingDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<IspOperationDto?> Handle(GetIspOperationQuery request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        var entity = await _ctx.InversionDeSujetoActivos
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.CompanyId == companyId, ct);
        return entity is null ? null : GetIspOperationsHandler.ToDto(entity);
    }
}

public sealed class CreateIspOperationHandler : IRequestHandler<CreateIspOperationCommand, IspOperationDto>
{
    private readonly IAccountingDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CreateIspOperationHandler(IAccountingDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<IspOperationDto> Handle(CreateIspOperationCommand request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        var isReverseCharge = IspRules.AppliesReverseCharge(request.SupplierCountryCode);
        var vatAmount = Math.Round(request.VatableBase * (request.VatRate / 100m), 2);

        var entity = new InversionDeSujetoActivo
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            SupplierCountryCode = request.SupplierCountryCode.ToUpperInvariant(),
            VatableBase = request.VatableBase,
            VatRate = request.VatRate,
            VatAmount = vatAmount,
            IsReverseCharge = isReverseCharge,
            DeclarationDate = DateTime.UtcNow
        };

        _ctx.InversionDeSujetoActivos.Add(entity);
        await _ctx.SaveChangesAsync(ct);

        return GetIspOperationsHandler.ToDto(entity) with { Status = "Created" };
    }
}

public sealed class CalculateIspVatHandler : IRequestHandler<CalculateIspVatCommand, IspVatCalculationDto>
{
    private readonly IAccountingDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CalculateIspVatHandler(IAccountingDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<IspVatCalculationDto> Handle(CalculateIspVatCommand request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        var entity = await _ctx.InversionDeSujetoActivos
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.CompanyId == companyId, ct)
            ?? throw new InvalidOperationException("Operación ISP no encontrada.");

        var invoiceVat = Math.Round(request.Amount * (entity.VatRate / 100m), 2);
        var ispApplied = entity.IsReverseCharge;

        return new IspVatCalculationDto(
            entity.Id,
            invoiceVat,
            ispApplied,
            ispApplied ? 0m : invoiceVat,
            ispApplied ? "IVA con ISP aplicado (autoliquidación)" : "IVA estándar sin ISP",
            "Calculated");
    }
}

internal static class IspRules
{
    private static readonly HashSet<string> EuCountries = new(StringComparer.OrdinalIgnoreCase)
    {
        "AT", "BE", "BG", "CY", "CZ", "DE", "DK", "EE", "EL", "ES", "FI", "FR",
        "HR", "HU", "IE", "IT", "LT", "LU", "LV", "MT", "NL", "PL", "PT", "RO",
        "SE", "SI", "SK"
    };

    public static bool AppliesReverseCharge(string countryCode) =>
        EuCountries.Contains(countryCode.Trim().ToUpperInvariant())
        && !string.Equals(countryCode.Trim(), "ES", StringComparison.OrdinalIgnoreCase);
}
