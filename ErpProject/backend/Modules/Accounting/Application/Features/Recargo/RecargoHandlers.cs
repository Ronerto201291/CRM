using Erp.Application.Common.Interfaces;
using Erp.Application.Common.Validation;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Accounting.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Application.Features.Recargo;

public record GetRecargosQuery(int Year, int Quarter) : IRequest<GetRecargosResult>;

public record GetRecargoByIdQuery(Guid Id) : IRequest<GetRecargoByIdResult>;

public record CreateRecargoCommand(
    string SupplierVat,
    bool SupplierIsRE,
    decimal BaseAmount,
    decimal RechargeRate) : IRequest<CreateRecargoResult>;

public record GenerateRecargoModelo303Command(Guid InvoiceId) : IRequest<RecargoModelo303Result>;

public class GetRecargosResult
{
    public string Period { get; set; } = string.Empty;
    public List<RecargoListItem> Recargos { get; set; } = [];
}

public class RecargoListItem
{
    public Guid Id { get; set; }
    public Guid? InvoiceId { get; set; }
    public string Source { get; set; } = "Invoice";
    public string InvoiceNumber { get; set; } = string.Empty;
    public string ClientTaxId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public decimal BaseAmount { get; set; }
    public decimal SurchargeRate { get; set; }
    public decimal SurchargeAmount { get; set; }
    public DateTime InvoiceDate { get; set; }
}

public class GetRecargoByIdResult
{
    public Guid Id { get; set; }
    public Guid? InvoiceId { get; set; }
    public string Source { get; set; } = "Invoice";
    public string InvoiceNumber { get; set; } = string.Empty;
    public string SupplierVat { get; set; } = string.Empty;
    public decimal BaseAmount { get; set; }
    public decimal SurchargeRate { get; set; }
    public decimal SurchargeAmount { get; set; }
    public string Modelo303Status { get; set; } = "Pending";
    public string Status { get; set; } = "Active";
}

public class CreateRecargoResult
{
    public Guid Id { get; set; }
    public string SupplierVat { get; set; } = string.Empty;
    public bool SupplierIsRE { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal RechargeRate { get; set; }
    public decimal RechargeAmount { get; set; }
    public string Status { get; set; } = "Created";
    public string Message { get; set; } = string.Empty;
}

public class RecargoModelo303Result
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string Modelo { get; set; } = "303";
    public List<RecargoModelo303Line> Recargo { get; set; } = [];
    public RecargoModelo303Totals Totales { get; set; } = new();
    public List<RecargoModelo303Casilla> Casillas { get; set; } = [];
}

public class RecargoModelo303Line
{
    public string Tipo { get; set; } = string.Empty;
    public decimal BaseImponible { get; set; }
    public decimal CuotaRecargo { get; set; }
    public decimal SurchargeRate { get; set; }
}

public class RecargoModelo303Totals
{
    public decimal TotalBaseImponible { get; set; }
    public decimal TotalCuotaRecargo { get; set; }
}

public class RecargoModelo303Casilla
{
    public string CasBase { get; set; } = string.Empty;
    public string CasCuota { get; set; } = string.Empty;
    public decimal SurchargeRate { get; set; }
}

public class GetRecargosHandler : IRequestHandler<GetRecargosQuery, GetRecargosResult>
{
    private readonly IRecargoInvoiceReader _reader;
    private readonly IAccountingDbContext _accounting;
    private readonly ITenantContext _tenant;

    public GetRecargosHandler(
        IRecargoInvoiceReader reader,
        IAccountingDbContext accounting,
        ITenantContext tenant)
    {
        _reader = reader;
        _accounting = accounting;
        _tenant = tenant;
    }

    public async Task<GetRecargosResult> Handle(GetRecargosQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var (from, to) = RecargoPeriodHelper.QuarterRange(request.Year, request.Quarter);

        var invoices = await _reader.GetLockedInvoicesWithRecargoAsync(tenantId, from, to, ct);

        var fromInvoices = invoices.Select(i => new RecargoListItem
        {
            Id = i.Id,
            InvoiceId = i.Id,
            Source = "Invoice",
            InvoiceNumber = i.Number,
            ClientTaxId = i.ClientNif,
            ClientName = i.ClientName,
            BaseAmount = i.Subtotal,
            SurchargeRate = i.Lines.First().SurchargeRate,
            SurchargeAmount = i.Lines.Sum(l => l.SurchargeAmount),
            InvoiceDate = i.IssueDate,
        });

        var manual = await _accounting.RecargoDEquivalencias
            .Where(r => r.CompanyId == tenantId
                     && r.CreatedAt >= from && r.CreatedAt < to
                     && r.InvoiceId == null)
            .AsNoTracking()
            .ToListAsync(ct);

        var fromManual = manual.Select(r => new RecargoListItem
        {
            Id = r.Id,
            InvoiceId = null,
            Source = "Manual",
            InvoiceNumber = $"RE-{r.Id.ToString()[..8]}",
            ClientTaxId = r.SupplierVatNumber,
            ClientName = r.SupplierIsRE ? "Proveedor RE" : "Proveedor",
            BaseAmount = r.Base,
            SurchargeRate = r.RechargeRate,
            SurchargeAmount = r.RechargeAmount,
            InvoiceDate = r.CreatedAt,
        });

        return new GetRecargosResult
        {
            Period = $"T{request.Quarter} {request.Year}",
            Recargos = fromInvoices.Concat(fromManual)
                .OrderByDescending(r => r.InvoiceDate)
                .ToList(),
        };
    }
}

public class GetRecargoByIdHandler : IRequestHandler<GetRecargoByIdQuery, GetRecargoByIdResult>
{
    private readonly IRecargoInvoiceReader _reader;
    private readonly IAccountingDbContext _accounting;
    private readonly ITenantContext _tenant;

    public GetRecargoByIdHandler(
        IRecargoInvoiceReader reader,
        IAccountingDbContext accounting,
        ITenantContext tenant)
    {
        _reader = reader;
        _accounting = accounting;
        _tenant = tenant;
    }

    public async Task<GetRecargoByIdResult> Handle(GetRecargoByIdQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");

        var invoice = await _reader.GetInvoiceWithRecargoAsync(tenantId, request.Id, ct);
        if (invoice is not null)
        {
            var line = invoice.Lines.First();
            return new GetRecargoByIdResult
            {
                Id = invoice.Id,
                InvoiceId = invoice.Id,
                Source = "Invoice",
                InvoiceNumber = invoice.Number,
                SupplierVat = invoice.ClientNif,
                BaseAmount = invoice.Subtotal,
                SurchargeRate = line.SurchargeRate,
                SurchargeAmount = line.SurchargeAmount,
            };
        }

        var manual = await _accounting.RecargoDEquivalencias
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.CompanyId == tenantId && r.Id == request.Id, ct)
            ?? throw new KeyNotFoundException(
                $"No se encontró recargo de equivalencia para el ID '{request.Id}'.");

        return new GetRecargoByIdResult
        {
            Id = manual.Id,
            InvoiceId = manual.InvoiceId,
            Source = manual.InvoiceId.HasValue ? "Invoice" : "Manual",
            InvoiceNumber = manual.InvoiceId.HasValue ? manual.InvoiceId.ToString()! : $"RE-{manual.Id.ToString()[..8]}",
            SupplierVat = manual.SupplierVatNumber,
            BaseAmount = manual.Base,
            SurchargeRate = manual.RechargeRate,
            SurchargeAmount = manual.RechargeAmount,
            Modelo303Status = manual.Modelo303Status,
            Status = manual.Modelo303Status == "Confirmed" ? "Confirmed" : "Active",
        };
    }
}

public class CreateRecargoHandler : IRequestHandler<CreateRecargoCommand, CreateRecargoResult>
{
    private static readonly HashSet<decimal> AllowedRates = [0.5m, 1.4m, 5.2m];

    private readonly IAccountingDbContext _accounting;
    private readonly ITenantContext _tenant;

    public CreateRecargoHandler(IAccountingDbContext accounting, ITenantContext tenant)
    {
        _accounting = accounting;
        _tenant = tenant;
    }

    public async Task<CreateRecargoResult> Handle(CreateRecargoCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");

        if (request.BaseAmount <= 0)
            throw new InvalidOperationException("La base imponible debe ser mayor que cero.");

        if (!AllowedRates.Contains(request.RechargeRate))
        {
            throw new InvalidOperationException(
                "Tasa de recargo no válida. Valores permitidos: 0,5%, 1,4% o 5,2%.");
        }

        if (!string.IsNullOrWhiteSpace(request.SupplierVat)
            && !SpanishTaxIdValidator.IsValid(request.SupplierVat))
        {
            throw new InvalidOperationException("NIF/CIF del proveedor no válido.");
        }

        var rechargeAmount = Math.Round(request.BaseAmount * (request.RechargeRate / 100m), 2);

        var entity = new RecargoDEquivalencia
        {
            CompanyId = tenantId,
            SupplierVatNumber = request.SupplierVat.Trim().ToUpperInvariant(),
            SupplierIsRE = request.SupplierIsRE,
            Base = request.BaseAmount,
            RechargeRate = request.RechargeRate,
            RechargeAmount = rechargeAmount,
            IsEndToEnd = false,
            Modelo303Status = "Pending",
        };

        _accounting.RecargoDEquivalencias.Add(entity);
        await _accounting.SaveChangesAsync(ct);

        return new CreateRecargoResult
        {
            Id = entity.Id,
            SupplierVat = entity.SupplierVatNumber,
            SupplierIsRE = entity.SupplierIsRE,
            BaseAmount = entity.Base,
            RechargeRate = entity.RechargeRate,
            RechargeAmount = entity.RechargeAmount,
            Message = "Recargo de equivalencia registrado",
        };
    }
}

public class GenerateRecargoModelo303Handler : IRequestHandler<GenerateRecargoModelo303Command, RecargoModelo303Result>
{
    private readonly IRecargoInvoiceReader _reader;
    private readonly ITenantContext _tenant;

    public GenerateRecargoModelo303Handler(IRecargoInvoiceReader reader, ITenantContext tenant)
    {
        _reader = reader;
        _tenant = tenant;
    }

    public async Task<RecargoModelo303Result> Handle(GenerateRecargoModelo303Command request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");

        var invoice = await _reader.GetInvoiceAsync(tenantId, request.InvoiceId, ct)
            ?? throw new KeyNotFoundException($"Factura '{request.InvoiceId}' no encontrada.");

        if (!invoice.IsLocked)
            throw new InvalidOperationException(
                "Solo se puede generar Modelo 303 para facturas bloqueadas (IsLocked=true).");

        var recargoLines = invoice.Lines.ToList();
        if (recargoLines.Count == 0)
            throw new InvalidOperationException("La factura no tiene recargo de equivalencia.");

        var bySurchargeRate = recargoLines
            .GroupBy(l => l.SurchargeRate)
            .Select(g => new RecargoModelo303Line
            {
                Tipo = $"Recargo {g.Key:F1}%",
                BaseImponible = Math.Round(g.Sum(l => l.LineTotal), 2),
                CuotaRecargo = Math.Round(g.Sum(l => l.SurchargeAmount), 2),
                SurchargeRate = g.Key,
            })
            .ToList();

        return new RecargoModelo303Result
        {
            InvoiceId = invoice.Id,
            InvoiceNumber = invoice.Number,
            Recargo = bySurchargeRate,
            Totales = new RecargoModelo303Totals
            {
                TotalBaseImponible = bySurchargeRate.Sum(r => r.BaseImponible),
                TotalCuotaRecargo = bySurchargeRate.Sum(r => r.CuotaRecargo),
            },
            Casillas = bySurchargeRate.Select(r => new RecargoModelo303Casilla
            {
                CasBase = r.SurchargeRate switch { 1.4m => "31", 5.2m => "33", 14.1m => "35", _ => "36" },
                CasCuota = r.SurchargeRate switch { 1.4m => "32", 5.2m => "34", 14.1m => "36", _ => "36" },
                SurchargeRate = r.SurchargeRate,
            }).ToList(),
        };
    }
}

internal static class RecargoPeriodHelper
{
    public static (DateTime from, DateTime to) QuarterRange(int year, int q)
    {
        var startMonth = (q - 1) * 3 + 1;
        var from = new DateTime(year, startMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        return (from, from.AddMonths(3));
    }
}
