using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using MediatR;

namespace Erp.Modules.Accounting.Application.Features.Recargo;

// ── Queries / Commands ───────────────────────────────────────────────────────

public record GetRecargosQuery(int Year, int Quarter) : IRequest<GetRecargosResult>;

public record GetRecargoByIdQuery(Guid Id) : IRequest<GetRecargoByIdResult>;

public record CreateRecargoCommand(
    string SupplierVat,
    bool SupplierIsRE,
    decimal BaseAmount,
    decimal RechargeRate) : IRequest<CreateRecargoResult>;

public record GenerateRecargoModelo303Command(Guid InvoiceId) : IRequest<RecargoModelo303Result>;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public class GetRecargosResult
{
    public string Period { get; set; } = string.Empty;
    public List<RecargoListItem> Recargos { get; set; } = [];
}

public class RecargoListItem
{
    public Guid InvoiceId { get; set; }
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
    public Guid InvoiceId { get; set; }
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

// ── Handlers ─────────────────────────────────────────────────────────────────

public class GetRecargosHandler : IRequestHandler<GetRecargosQuery, GetRecargosResult>
{
    private readonly IRecargoInvoiceReader _reader;
    private readonly ITenantContext _tenant;

    public GetRecargosHandler(IRecargoInvoiceReader reader, ITenantContext tenant)
    {
        _reader = reader;
        _tenant = tenant;
    }

    public async Task<GetRecargosResult> Handle(GetRecargosQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var (from, to) = RecargoPeriodHelper.QuarterRange(request.Year, request.Quarter);

        var invoices = await _reader.GetLockedInvoicesWithRecargoAsync(tenantId, from, to, ct);

        return new GetRecargosResult
        {
            Period = $"T{request.Quarter} {request.Year}",
            Recargos = invoices.Select(i => new RecargoListItem
            {
                InvoiceId = i.Id,
                InvoiceNumber = i.Number,
                ClientTaxId = i.ClientNif,
                ClientName = i.ClientName,
                BaseAmount = i.Subtotal,
                SurchargeRate = i.Lines.First().SurchargeRate,
                SurchargeAmount = i.Lines.Sum(l => l.SurchargeAmount),
                InvoiceDate = i.IssueDate,
            }).ToList(),
        };
    }
}

public class GetRecargoByIdHandler : IRequestHandler<GetRecargoByIdQuery, GetRecargoByIdResult>
{
    private readonly IRecargoInvoiceReader _reader;
    private readonly ITenantContext _tenant;

    public GetRecargoByIdHandler(IRecargoInvoiceReader reader, ITenantContext tenant)
    {
        _reader = reader;
        _tenant = tenant;
    }

    public async Task<GetRecargoByIdResult> Handle(GetRecargoByIdQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");

        var invoice = await _reader.GetInvoiceWithRecargoAsync(tenantId, request.Id, ct)
            ?? throw new KeyNotFoundException($"No se encontró factura con recargo de equivalencia para el ID '{request.Id}'.");

        var line = invoice.Lines.First();
        return new GetRecargoByIdResult
        {
            InvoiceId = invoice.Id,
            InvoiceNumber = invoice.Number,
            SupplierVat = invoice.ClientNif,
            BaseAmount = invoice.Subtotal,
            SurchargeRate = line.SurchargeRate,
            SurchargeAmount = line.SurchargeAmount,
        };
    }
}

public class CreateRecargoHandler : IRequestHandler<CreateRecargoCommand, CreateRecargoResult>
{
    public Task<CreateRecargoResult> Handle(CreateRecargoCommand request, CancellationToken ct)
    {
        var rechargeAmount = request.BaseAmount * (request.RechargeRate / 100);

        return Task.FromResult(new CreateRecargoResult
        {
            Id = Guid.NewGuid(),
            SupplierVat = request.SupplierVat,
            SupplierIsRE = request.SupplierIsRE,
            BaseAmount = request.BaseAmount,
            RechargeRate = request.RechargeRate,
            RechargeAmount = rechargeAmount,
            Message = "Recargo de equivalencia registrado",
        });
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
            throw new InvalidOperationException("Solo se puede generar Modelo 303 para facturas bloqueadas (IsLocked=true).");

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

        var totalBase = bySurchargeRate.Sum(r => r.BaseImponible);
        var totalCuota = bySurchargeRate.Sum(r => r.CuotaRecargo);

        return new RecargoModelo303Result
        {
            InvoiceId = invoice.Id,
            InvoiceNumber = invoice.Number,
            Recargo = bySurchargeRate,
            Totales = new RecargoModelo303Totals
            {
                TotalBaseImponible = totalBase,
                TotalCuotaRecargo = totalCuota,
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
        var to = from.AddMonths(3);
        return (from, to);
    }
}
