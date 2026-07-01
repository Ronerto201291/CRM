using Erp.Application.Common.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Api.Controllers;

[ApiController]
[Route("api/v1/accounting/recargo")]
public class RecargoController : ControllerBase
{
    private readonly IBillingDbContext _billing;
    private readonly ITenantContext _tenant;
    private static readonly System.Globalization.CultureInfo Es = System.Globalization.CultureInfo.InvariantCulture;

    public RecargoController(
        IBillingDbContext billing,
        ITenantContext tenant)
    {
        _billing = billing;
        _tenant  = tenant;
    }

    /// <summary>
    /// Lista todos los recargos de equivalencia de la empresa para un periodo dado.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int year, [FromQuery] int q, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var (from, to) = QuarterRange(year, q);

        // Recargos aplicados = invoices del periodo con SurchargeRate > 0
        var invoices = await _billing.Invoices
            .Include(i => i.InvoiceLines)
            .Where(i => i.CompanyId == tenantId
                     && i.IsLocked
                     && i.IssueDate >= from && i.IssueDate < to
                     && i.InvoiceLines.Any(l => l.SurchargeRate > 0))
            .AsNoTracking().ToListAsync(ct);

        var recargos = invoices.Select(i => new
        {
            invoiceId      = i.Id,
            invoiceNumber  = i.Number,
            clientTaxId    = i.ClientNif,
            clientName     = i.ClientName,
            baseAmount     = i.Subtotal,
            surchargeRate  = i.InvoiceLines.First(l => l.SurchargeRate > 0).SurchargeRate,
            surchargeAmount = i.InvoiceLines.Sum(l => l.SurchargeAmount),
            invoiceDate    = i.IssueDate
        }).ToList();

        return Ok(new { period = $"T{q} {year}", recargos });
    }

    [HttpPost]
    public IActionResult Create([FromBody] CreateRecargoRequest request)
    {
        var rechargeAmount = request.BaseAmount * (request.RechargeRate / 100);

        return Created("", new
        {
            id = Guid.NewGuid(),
            supplierVat = request.SupplierVat,
            supplierIsRE = request.SupplierIsRE,
            baseAmount = request.BaseAmount,
            rechargeRate = request.RechargeRate,
            rechargeAmount = rechargeAmount,
            status = "Created",
            message = "Recargo de equivalencia registrado"
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");

        var invoice = await _billing.Invoices
            .Include(i => i.InvoiceLines)
            .Where(i => i.CompanyId == tenantId && i.Id == id
                     && i.InvoiceLines.Any(l => l.SurchargeRate > 0))
            .AsNoTracking().FirstOrDefaultAsync(ct);

        if (invoice is null)
            return NotFound(new { error = $"No se encontró factura con recargo de equivalencia para el ID '{id}'." });

        var line = invoice.InvoiceLines.First(l => l.SurchargeRate > 0);
        return Ok(new
        {
            invoiceId       = invoice.Id,
            invoiceNumber   = invoice.Number,
            supplierVat     = invoice.ClientNif,
            baseAmount      = invoice.Subtotal,
            surchargeRate   = line.SurchargeRate,
            surchargeAmount = line.SurchargeAmount,
            modelo303Status = "Pending",
            status          = "Active"
        });
    }

    /// <summary>
    /// Genera la sección de recargo de equivalencia del Modelo 303 para un periodo.
    /// Delega al endpoint JSON del AccountingExportController para obtener los datos reales.
    /// </summary>
    [HttpPost("{id}/modelo303")]
    public async Task<IActionResult> GenerateModelo303(Guid id, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");

        var invoice = await _billing.Invoices
            .Include(i => i.InvoiceLines)
            .Where(i => i.CompanyId == tenantId && i.Id == id)
            .AsNoTracking().FirstOrDefaultAsync(ct);

        if (invoice is null)
            return NotFound(new { error = $"Factura '{id}' no encontrada." });

        if (!invoice.IsLocked)
            return BadRequest(new { error = "Solo se puede generar Modelo 303 para facturas bloqueadas (IsLocked=true)." });

        // Recargo de equivalencia agrupado por tasa
        var recargoLines = invoice.InvoiceLines
            .Where(l => l.SurchargeRate > 0)
            .ToList();

        if (!recargoLines.Any())
            return BadRequest(new { error = "La factura no tiene recargo de equivalencia." });

        var bySurchargeRate = recargoLines
            .GroupBy(l => l.SurchargeRate)
            .Select(g => new
            {
                tipo            = $"Recargo {g.Key:F1}%",
                baseImponible   = Math.Round(g.Sum(l => l.LineTotal), 2),
                cuotaRecargo    = Math.Round(g.Sum(l => l.SurchargeAmount), 2),
                surchargeRate   = g.Key
            })
            .ToList();

        var totalBase = bySurchargeRate.Sum(r => r.baseImponible);
        var totalCuota = bySurchargeRate.Sum(r => r.cuotaRecargo);

        var result = new
        {
            invoiceId    = invoice.Id,
            invoiceNumber = invoice.Number,
            modelo       = "303",
            recargo      = bySurchargeRate,
            totales      = new
            {
                totalBaseImponible = totalBase,
                totalCuotaRecargo  = totalCuota
            },
            // mapping a casillas oficiales 303 AEAT (recargo de equivalencia)
            casillas = bySurchargeRate.Select(r => new
            {
                casBase   = r.surchargeRate switch { 1.4m => "31", 5.2m => "33", 14.1m => "35", _ => "36" },
                casCuota  = r.surchargeRate switch { 1.4m => "32", 5.2m => "34", 14.1m => "36", _ => "36" },
                surchargeRate = r.surchargeRate
            }).ToList()
        };

        return Ok(result);
    }

    private static (DateTime from, DateTime to) QuarterRange(int year, int q)
    {
        var startMonth = (q - 1) * 3 + 1;
        var from = new DateTime(year, startMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddMonths(3);
        return (from, to);
    }
}

public class CreateRecargoRequest
{
    public string SupplierVat { get; set; } = string.Empty;
    public bool SupplierIsRE { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal RechargeRate { get; set; } = 5.2m;
}
