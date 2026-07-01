using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Accounting;
using Erp.Modules.Accounting.Application.Commands;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Accounting.Application.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Erp.Modules.Accounting.Application.Handlers;

// ── CREATE ──────────────────────────────────────────────────────────────────
public class CreateFixedAssetHandler : IRequestHandler<CreateFixedAssetCommand, Guid>
{
    private readonly IAccountingDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CreateFixedAssetHandler(IAccountingDbContext ctx, ITenantContext tenant)
    {
        _ctx    = ctx;
        _tenant = tenant;
    }

    public async Task<Guid> Handle(CreateFixedAssetCommand req, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");

        var asset = new FixedAsset
        {
            Id                         = Guid.NewGuid(),
            CompanyId                  = companyId,
            AssetCode                  = req.AssetCode,
            Name                       = req.Name,
            Description                = req.Description,
            AcquisitionDate            = req.AcquisitionDate,
            CommissioningDate          = req.CommissioningDate,
            AcquisitionCost            = req.AcquisitionCost,
            ResidualValue              = req.ResidualValue,
            UsefulLifeYears            = req.UsefulLifeYears,
            AmortizationMethod         = req.AmortizationMethod,
            AssetAccountCode           = req.AssetAccountCode,
            DepreciationAccountCode    = req.DepreciationAccountCode,
            AccumDepreciationAccountCode = req.AccumDepreciationAccountCode,
            Notes                      = req.Notes,
            Status                     = "Active",
            CreatedAt                  = DateTime.UtcNow
        };

        _ctx.FixedAssets.Add(asset);
        await _ctx.SaveChangesAsync(ct);
        return asset.Id;
    }
}

// ── UPDATE ───────────────────────────────────────────────────────────────────
public class UpdateFixedAssetHandler : IRequestHandler<UpdateFixedAssetCommand>
{
    private readonly IAccountingDbContext _ctx;

    public UpdateFixedAssetHandler(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task Handle(UpdateFixedAssetCommand req, CancellationToken ct)
    {
        var asset = await _ctx.FixedAssets.FindAsync([req.Id], ct)
            ?? throw new KeyNotFoundException($"Activo {req.Id} no encontrado.");
        if (asset.Status == "Disposed")
            throw new InvalidOperationException("No se puede modificar un activo dado de baja.");

        asset.Name            = req.Name;
        asset.Description     = req.Description;
        asset.ResidualValue   = req.ResidualValue;
        asset.UsefulLifeYears = req.UsefulLifeYears;
        asset.Notes           = req.Notes;
        asset.UpdatedAt       = DateTime.UtcNow;

        await _ctx.SaveChangesAsync(ct);
    }
}

// ── DISPOSE ──────────────────────────────────────────────────────────────────
public class DisposeFixedAssetHandler : IRequestHandler<DisposeFixedAssetCommand>
{
    private readonly IAccountingDbContext _ctx;

    public DisposeFixedAssetHandler(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task Handle(DisposeFixedAssetCommand req, CancellationToken ct)
    {
        var asset = await _ctx.FixedAssets.FindAsync([req.Id], ct)
            ?? throw new KeyNotFoundException($"Activo {req.Id} no encontrado.");
        if (asset.Status == "Disposed")
            throw new InvalidOperationException("El activo ya está dado de baja.");

        asset.Status     = "Disposed";
        asset.DisposedAt = req.DisposedAt;
        asset.Notes      = req.Notes ?? asset.Notes;
        asset.UpdatedAt  = DateTime.UtcNow;

        await _ctx.SaveChangesAsync(ct);
    }
}

// ── POST MONTHLY AMORTIZATION ────────────────────────────────────────────────
/// <summary>
/// Genera el asiento de dotación de amortización para un activo y mes concreto.
/// Idempotente: si ya existe un asiento para ese activo/mes, devuelve null.
///
/// Asiento:
///   Debe  68x (DepreciationAccountCode)      = cuota mensual
///   Haber 28x (AccumDepreciationAccountCode) = cuota mensual
/// </summary>
public class PostMonthlyAmortizationHandler : IRequestHandler<PostMonthlyAmortizationCommand, Guid?>
{
    private readonly IAccountingDbContext _ctx;
    private readonly ILogger<PostMonthlyAmortizationHandler> _logger;

    public PostMonthlyAmortizationHandler(IAccountingDbContext ctx, ILogger<PostMonthlyAmortizationHandler> logger)
    {
        _ctx    = ctx;
        _logger = logger;
    }

    public async Task<Guid?> Handle(PostMonthlyAmortizationCommand req, CancellationToken ct)
    {
        var asset = await _ctx.FixedAssets.FindAsync([req.AssetId], ct)
            ?? throw new KeyNotFoundException($"Activo {req.AssetId} no encontrado.");

        if (asset.Status != "Active") return null;

        var monthStart = new DateTime(req.Year, req.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // Idempotencia: comprobar si ya existe asiento para este activo/mes
        var reference = $"AMOR-{asset.AssetCode}-{req.Year:D4}{req.Month:D2}";
        var exists = await _ctx.JournalEntries
            .AnyAsync(j => j.Reference == reference && j.CompanyId == asset.CompanyId, ct);
        if (exists)
        {
            _logger.LogWarning("Asiento de amortización {Ref} ya existe. Ignorando.", reference);
            return null;
        }

        // No comenzar antes de la fecha de puesta en funcionamiento
        if (monthStart < new DateTime(asset.CommissioningDate.Year, asset.CommissioningDate.Month, 1, 0, 0, 0, DateTimeKind.Utc))
            return null;

        // Calcular cuota
        var depreciableAmount = asset.AcquisitionCost - asset.ResidualValue;
        if (depreciableAmount <= 0) return null;

        var monthlyQuota  = depreciableAmount / (asset.UsefulLifeYears * 12m);
        var remaining     = depreciableAmount - asset.AccumulatedDepreciation;
        if (remaining <= 0) return null;

        var quota = Math.Min(monthlyQuota, remaining);
        quota     = Math.Round(quota, 2);
        if (quota <= 0) return null;

        // Obtener cuentas contables
        var acctDepr = await _ctx.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.CompanyId == asset.CompanyId && a.Code == asset.DepreciationAccountCode, ct)
            ?? throw new InvalidOperationException($"Cuenta {asset.DepreciationAccountCode} no encontrada.");

        var acctAccum = await _ctx.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.CompanyId == asset.CompanyId && a.Code == asset.AccumDepreciationAccountCode, ct)
            ?? throw new InvalidOperationException($"Cuenta {asset.AccumDepreciationAccountCode} no encontrada.");

        // Crear asiento
        var entry = new JournalEntry
        {
            Id          = Guid.NewGuid(),
            CompanyId   = asset.CompanyId,
            Date        = monthStart,
            Reference   = reference,
            Description = $"Amortización {asset.Name} ({asset.AssetCode}) — {req.Month:D2}/{req.Year}",
            SourceType  = "Amortization",
            SourceId    = asset.Id,
            IsPosted    = true,
            PostedAt    = DateTime.UtcNow,
            JournalEntryLines = new List<JournalEntryLine>
            {
                new() {
                    Id = Guid.NewGuid(), JournalEntryId = Guid.Empty,
                    AccountId = acctDepr.Id, AccountCode = acctDepr.Code, AccountName = acctDepr.Name,
                    Debit = quota, Credit = 0
                },
                new() {
                    Id = Guid.NewGuid(), JournalEntryId = Guid.Empty,
                    AccountId = acctAccum.Id, AccountCode = acctAccum.Code, AccountName = acctAccum.Name,
                    Debit = 0, Credit = quota
                }
            }
        };

        foreach (var line in entry.JournalEntryLines)
            line.JournalEntryId = entry.Id;

        // Actualizar activo
        asset.AccumulatedDepreciation += quota;
        asset.LastAmortizationDate     = monthStart;
        if (asset.AccumulatedDepreciation >= depreciableAmount - 0.01m)
            asset.Status = "FullyAmortized";
        asset.UpdatedAt = DateTime.UtcNow;

        _ctx.JournalEntries.Add(entry);
        await _ctx.SaveChangesAsync(ct);

        _logger.LogInformation("Amortización {Ref}: {Quota:F2} € — Activo {Code}", reference, quota, asset.AssetCode);
        return entry.Id;
    }
}

// ── QUERIES ───────────────────────────────────────────────────────────────────
public class GetFixedAssetsHandler : IRequestHandler<GetFixedAssetsQuery, List<FixedAssetDto>>
{
    private readonly IAccountingDbContext _ctx;

    public GetFixedAssetsHandler(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task<List<FixedAssetDto>> Handle(GetFixedAssetsQuery req, CancellationToken ct)
    {
        var q = _ctx.FixedAssets.AsNoTracking();
        if (!string.IsNullOrEmpty(req.Status))
            q = q.Where(a => a.Status == req.Status);

        return await q.OrderBy(a => a.AssetCode)
            .Select(a => ToDto(a))
            .ToListAsync(ct);
    }

    private static FixedAssetDto ToDto(FixedAsset a) => new(
        a.Id, a.AssetCode, a.Name, a.Description,
        a.AcquisitionDate, a.CommissioningDate,
        a.AcquisitionCost, a.ResidualValue, a.UsefulLifeYears,
        a.AmortizationMethod, a.AssetAccountCode,
        a.DepreciationAccountCode, a.AccumDepreciationAccountCode,
        a.AccumulatedDepreciation,
        a.AcquisitionCost - a.AccumulatedDepreciation,
        (a.UsefulLifeYears > 0 && (a.AcquisitionCost - a.ResidualValue) > 0)
            ? Math.Round((a.AcquisitionCost - a.ResidualValue) / (a.UsefulLifeYears * 12m), 2)
            : 0m,
        a.LastAmortizationDate, a.Status, a.DisposedAt, a.Notes, a.CreatedAt
    );
}

public class GetFixedAssetHandler : IRequestHandler<GetFixedAssetQuery, FixedAssetDto?>
{
    private readonly IAccountingDbContext _ctx;

    public GetFixedAssetHandler(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task<FixedAssetDto?> Handle(GetFixedAssetQuery req, CancellationToken ct)
    {
        var a = await _ctx.FixedAssets.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == req.Id, ct);
        if (a is null) return null;

        return new FixedAssetDto(
            a.Id, a.AssetCode, a.Name, a.Description,
            a.AcquisitionDate, a.CommissioningDate,
            a.AcquisitionCost, a.ResidualValue, a.UsefulLifeYears,
            a.AmortizationMethod, a.AssetAccountCode,
            a.DepreciationAccountCode, a.AccumDepreciationAccountCode,
            a.AccumulatedDepreciation,
            a.AcquisitionCost - a.AccumulatedDepreciation,
            (a.UsefulLifeYears > 0 && (a.AcquisitionCost - a.ResidualValue) > 0)
                ? Math.Round((a.AcquisitionCost - a.ResidualValue) / (a.UsefulLifeYears * 12m), 2)
                : 0m,
            a.LastAmortizationDate, a.Status, a.DisposedAt, a.Notes, a.CreatedAt
        );
    }
}
