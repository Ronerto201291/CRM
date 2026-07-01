using Erp.Application.Common.Interfaces;
using Erp.Infrastructure.Fiscal;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using Erp.Modules.Crm.Application.Interfaces;
using Erp.Modules.Expenses.Application.Interfaces;
using Erp.Modules.Payroll.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace Erp.Modules.Accounting.Api.Controllers;

/// <summary>
/// Exports AEAT-format accounting books as downloadable CSV/TXT files.
///   GET /api/accounting/export/libro-diario   — Libro Diario (daily journal)
///   GET /api/accounting/export/modelo303      — Modelo 303 (IVA trimestral)
///   GET /api/accounting/export/modelo347      — Modelo 347 (operaciones con terceros)
/// </summary>
[ApiController]
[Route("api/accounting/export")]
[Authorize]
public class AccountingExportController : ControllerBase
{
    private readonly IAccountingDbContext  _accounting;
    private readonly IExpensesDbContext    _expenses;
    private readonly IApplicationDbContext _app;
    private readonly IBillingDbContext     _billing;
    private readonly ICrmDbContext         _crm;
    private readonly IPayrollDbContext     _payroll;
    private readonly ITenantContext        _tenantContext;

    private static readonly CultureInfo Es = CultureInfo.InvariantCulture;

    private static IActionResult FileFiscal(ControllerBase c, byte[] bytes, string contentType, string downloadName, string disclaimer)
    {
        FiscalExportHeaders.MarkAsNonOfficial(c.Response.Headers, disclaimer);
        return c.File(bytes, contentType, downloadName);
    }

    public AccountingExportController(
        IAccountingDbContext  accounting,
        IExpensesDbContext    expenses,
        IApplicationDbContext app,
        IBillingDbContext     billing,
        ICrmDbContext         crm,
        IPayrollDbContext     payroll,
        ITenantContext        tenantContext)
    {
        _accounting    = accounting;
        _expenses      = expenses;
        _app           = app;
        _billing       = billing;
        _crm           = crm;
        _payroll       = payroll;
        _tenantContext = tenantContext;
    }

    // ─── Libro Diario ─────────────────────────────────────────────────────────

    [HttpGet("libro-diario")]
    public async Task<IActionResult> ExportLibroDiario([FromQuery] int year, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var entries = await _accounting.JournalEntries
            .Include(e => e.JournalEntryLines).ThenInclude(l => l.Account)
            .Where(e => e.CompanyId == tenantId && e.Date.Year == year)
            .OrderBy(e => e.Date).AsNoTracking().ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("Fecha;Asiento;Cuenta;NombreCuenta;Concepto;Debe;Haber");
        foreach (var entry in entries)
        {
            foreach (var line in entry.JournalEntryLines.OrderBy(l => l.Account?.Code))
            {
                sb.AppendLine(string.Join(";",
                    entry.Date.ToString("dd/MM/yyyy"),
                    entry.Reference ?? entry.Id.ToString()[..8],
                    line.Account?.Code ?? string.Empty,
                    (line.Account?.Name ?? string.Empty).Replace(";", " "),
                    (entry.Description ?? string.Empty).Replace(";", " "),
                    line.Debit.ToString("F2", Es),
                    line.Credit.ToString("F2", Es)));
            }
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return FileFiscal(this, bytes, "text/csv", $"LibroDiario_{year}.csv",
            "CSV libro diario interno; no sustituye el libro oficial del Código de Comercio sin asesoría.");
    }

    // ─── Modelo 303 ───────────────────────────────────────────────────────────

    [HttpGet("modelo303")]
    public async Task<IActionResult> ExportModelo303([FromQuery] int year, [FromQuery] int q, CancellationToken ct)
    {
        if (q < 1 || q > 4)
            return BadRequest(new { error = "q must be 1–4 (quarter)" });

        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var company = await _app.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == tenantId, ct);
        var (from, to) = QuarterRange(year, q);

        var ivaDevengado = await _accounting.JournalEntryLines
            .Include(l => l.Account).Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.CompanyId == tenantId && l.Account.Code.StartsWith("477")
                && l.JournalEntry.Date >= from && l.JournalEntry.Date < to)
            .AsNoTracking().ToListAsync(ct);

        var ivaDeducible = await _accounting.JournalEntryLines
            .Include(l => l.Account).Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.CompanyId == tenantId && l.Account.Code.StartsWith("472")
                && l.JournalEntry.Date >= from && l.JournalEntry.Date < to)
            .AsNoTracking().ToListAsync(ct);

        var invoices = await _billing.Invoices
            .Include(i => i.InvoiceLines)
            .Where(i => i.CompanyId == tenantId && i.IsLocked && i.IssueDate >= from && i.IssueDate < to)
            .AsNoTracking().ToListAsync(ct);

        var allLines       = invoices.SelectMany(i => i.InvoiceLines).ToList();
        var nacionales     = allLines.Where(l => l.TipoOperacion == "Nacional");
        var byRate         = nacionales.GroupBy(l => l.TaxRate)
            .ToDictionary(g => g.Key, g => new { Base = g.Sum(l => l.LineTotal), Cuota = g.Sum(l => l.TaxAmount) });
        var bySurchargeRate = nacionales.Where(l => l.SurchargeRate > 0).GroupBy(l => l.SurchargeRate)
            .ToDictionary(g => g.Key, g => new { Base = g.Sum(l => l.LineTotal), Cuota = g.Sum(l => l.SurchargeAmount) });

        var intracom       = allLines.Where(l => l.TipoOperacion == "IntraComunitario").Sum(l => l.LineTotal);
        var exportaciones  = allLines.Where(l => l.TipoOperacion == "Exportacion").Sum(l => l.LineTotal);

        decimal totalIVADevengado  = Math.Round(byRate.Values.Sum(v => v.Cuota), 2);
        decimal totalRecargoDevengado = Math.Round(bySurchargeRate.Values.Sum(v => v.Cuota), 2);
        decimal totalDevengado     = totalIVADevengado + totalRecargoDevengado;
        decimal totalDeducible     = Math.Round(ivaDeducible.Sum(l => l.Debit), 2);
        decimal resultado          = totalDevengado - totalDeducible;

        var sb = new StringBuilder();
        sb.AppendLine($"MODELO 303 — IVA. Declaración trimestral {year} T{q}");
        sb.AppendLine($"NIF;{company?.TaxId}");
        sb.AppendLine($"Razón Social;{company?.Name}");
        sb.AppendLine($"Período;{year}-T{q} ({from:dd/MM/yyyy} – {to.AddDays(-1):dd/MM/yyyy})");

        sb.AppendLine(); sb.AppendLine("DEVENGADO — IVA NACIONAL");
        sb.AppendLine("Tipo;BaseImponible;Casilla_Base;Cuota;Casilla_Cuota");
        foreach (var (rate, vals) in byRate.OrderByDescending(r => r.Key))
        {
            var (casBase, casCuota) = RateToCasillas(rate);
            sb.AppendLine(string.Join(";", $"Nacional {rate:F0}%",
                vals.Base.ToString("F2", Es), casBase, vals.Cuota.ToString("F2", Es), casCuota));
        }

        if (bySurchargeRate.Count > 0)
        {
            sb.AppendLine(); sb.AppendLine("RECARGO DE EQUIVALENCIA");
            sb.AppendLine("Tipo;BaseImponible;Casilla_Base;Cuota;Casilla_Cuota");
            foreach (var (rate, vals) in bySurchargeRate.OrderByDescending(r => r.Key))
            {
                var (casBase, casCuota) = SurchargeRateToCasillas(rate);
                sb.AppendLine(string.Join(";", $"Recargo {rate:F1}%",
                    vals.Base.ToString("F2", Es), casBase, vals.Cuota.ToString("F2", Es), casCuota));
            }
        }

        if (intracom > 0 || exportaciones > 0)
        {
            sb.AppendLine(); sb.AppendLine("EXENTAS");
            if (intracom > 0)
                sb.AppendLine($"Entregas intracomunitarias (casilla 59);{intracom.ToString("F2", Es)}");
            if (exportaciones > 0)
                sb.AppendLine($"Exportaciones (casilla 60);{exportaciones.ToString("F2", Es)}");
        }

        sb.AppendLine(); sb.AppendLine($"Total IVA Devengado (casilla 27);{totalDevengado.ToString("F2", Es)}");
        sb.AppendLine(); sb.AppendLine("DEDUCIBLE");
        sb.AppendLine($"IVA Soportado corriente (casilla 28);{totalDeducible.ToString("F2", Es)}");
        sb.AppendLine(); sb.AppendLine("LIQUIDACIÓN");
        sb.AppendLine($"Resultado ({(resultado >= 0 ? "A INGRESAR" : "A COMPENSAR")}) (casilla 46);{Math.Abs(resultado).ToString("F2", Es)}");

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return FileFiscal(this, bytes, "text/csv", $"Modelo303_{year}_T{q}.csv",
            "CSV modelo 303 orientativo; validar con el modelo oficial y plataforma AEAT.");
    }

    // ─── Modelo 303 — JSON (datos estructurados) ──────────────────────────────

    /// <summary>
    /// GET /api/accounting/modelo-303?year=2026&amp;quarter=1
    /// Retorna los datos del Modelo 303 en formato JSON (para el frontend o integración externa).
    /// </summary>
    [HttpGet("/api/accounting/modelo-303")]
    public async Task<IActionResult> GetModelo303Json([FromQuery] int year, [FromQuery] int quarter, CancellationToken ct)
    {
        if (quarter < 1 || quarter > 4)
            return BadRequest(new { error = "quarter debe ser 1–4" });

        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var company  = await _app.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == tenantId, ct);
        var (from, to) = QuarterRange(year, quarter);

        var invoices = await _billing.Invoices
            .Include(i => i.InvoiceLines)
            .Where(i => i.CompanyId == tenantId && i.IsLocked && i.IssueDate >= from && i.IssueDate < to)
            .AsNoTracking().ToListAsync(ct);

        var allLines = invoices.SelectMany(i => i.InvoiceLines).ToList();

        // Devengado nacional: agrupado por tipo de IVA
        var nacional = allLines
            .Where(l => l.TipoOperacion == "Nacional")
            .GroupBy(l => l.TaxRate)
            .OrderByDescending(g => g.Key)
            .Select(g =>
            {
                var (casBase, casCuota) = RateToCasillas(g.Key);
                return new { tipo = $"Nacional {g.Key:F0}%", casBase, casCuota,
                    baseImponible = Math.Round(g.Sum(l => l.LineTotal), 2),
                    cuotaIVA      = Math.Round(g.Sum(l => l.TaxAmount),  2) };
            }).ToList();

        // Recargo de equivalencia: agrupado por tasa
        var recargo = allLines
            .Where(l => l.TipoOperacion == "Nacional" && l.SurchargeRate > 0)
            .GroupBy(l => l.SurchargeRate)
            .OrderByDescending(g => g.Key)
            .Select(g =>
            {
                var (casBase, casCuota) = SurchargeRateToCasillas(g.Key);
                return new { tipo = $"Recargo {g.Key:F1}%", casBase, casCuota,
                    baseImponible   = Math.Round(g.Sum(l => l.LineTotal),      2),
                    cuotaRecargo    = Math.Round(g.Sum(l => l.SurchargeAmount), 2) };
            }).ToList();

        // Exportaciones / intracomunitarias: exentas, solo base (sin cuota IVA)
        var intracom = Math.Round(allLines.Where(l => l.TipoOperacion == "IntraComunitario").Sum(l => l.LineTotal), 2);
        var exportac = Math.Round(allLines.Where(l => l.TipoOperacion == "Exportacion").Sum(l => l.LineTotal), 2);

        // IVA deducible (cuenta 472)
        var deducibleLines = await _accounting.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.CompanyId == tenantId
                     && l.AccountCode.StartsWith("472")
                     && l.JournalEntry.Date >= from && l.JournalEntry.Date < to)
            .AsNoTracking().ToListAsync(ct);

        var ivaDeducible   = Math.Round(deducibleLines.Sum(l => l.Debit), 2);
        var totalDevengado = Math.Round(
            (decimal)nacional.Sum(x => x.cuotaIVA) + (decimal)recargo.Sum(x => x.cuotaRecargo), 2);
        var resultado = totalDevengado - ivaDeducible;

        return Ok(new
        {
            year, quarter,
            period      = $"T{quarter} {year} ({from:dd/MM/yyyy} – {to.AddDays(-1):dd/MM/yyyy})",
            nif         = company?.TaxId,
            razonSocial = company?.Name,
            devengado = new
            {
                nacional, recargo,
                entregasIntracomunitarias = new { casilla = "59", baseImponible = intracom },
                exportaciones             = new { casilla = "60", baseImponible = exportac },
                totalDevengado            = new { casilla = "27", valor = totalDevengado },
            },
            deducible = new
            {
                operacionesCorrientes = new { casBase = "28", casCuota = "29", cuota = ivaDeducible },
                totalDeducible        = new { casilla = "45", valor = ivaDeducible },
            },
            liquidacion = new
            {
                casilla = "46",
                valor   = Math.Abs(resultado),
                tipo    = resultado >= 0 ? "AIngresar" : "ACompensar",
            }
        });
    }

    // ─── Modelo 111 — Retenciones IRPF (JSON) ─────────────────────────────────

    /// <summary>
    /// GET /api/accounting/modelo-111?year=2026&amp;quarter=1
    /// Modelo 111: retenciones e ingresos a cuenta de IRPF (actividades profesionales).
    /// Lista facturas emitidas con retención durante el trimestre.
    /// </summary>
    [HttpGet("/api/accounting/modelo-111")]
    public async Task<IActionResult> GetModelo111([FromQuery] int year, [FromQuery] int quarter, CancellationToken ct)
    {
        if (quarter < 1 || quarter > 4)
            return BadRequest(new { error = "quarter debe ser 1–4" });

        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var company  = await _app.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == tenantId, ct);
        var (from, to) = QuarterRange(year, quarter);

        var invoices = await _billing.Invoices
            .Where(i => i.CompanyId == tenantId && i.IsLocked
                     && i.IrpfAmount > 0
                     && i.IssueDate >= from && i.IssueDate < to)
            .AsNoTracking()
            .OrderBy(i => i.IssueDate)
            .Select(i => new
            {
                numero           = i.Number,
                fechaExpedicion  = i.IssueDate,
                clienteNif       = i.ClientNif,
                clienteNombre    = i.ClientName,
                baseRetencion    = i.Subtotal,
                tipoRetencion    = i.IrpfRate,
                importeRetencion = i.IrpfAmount,
            })
            .ToListAsync(ct);

        var baseTotal    = Math.Round(invoices.Sum(i => i.baseRetencion), 2);
        var importeTotal = Math.Round(invoices.Sum(i => i.importeRetencion), 2);

        var monthStart = (quarter - 1) * 3 + 1;
        var monthEnd   = quarter * 3;
        var nominas = await _payroll.PayrollLines
            .Include(l => l.Settlement)
            .Include(l => l.Employee)
            .Where(l => l.Settlement != null
                     && l.Settlement!.Year == year
                     && l.Settlement.Month >= monthStart && l.Settlement.Month <= monthEnd
                     && l.Settlement.Status == "Final")
            .AsNoTracking()
            .OrderBy(l => l.Employee!.TaxId)
            .Select(l => new
            {
                nif              = l.Employee!.TaxId,
                nombre           = l.Employee.FullName,
                mes              = l.Settlement!.Month,
                baseRetencion    = l.IrpfBase,
                tipoRetencion    = l.IrpfRate,
                importeRetencion = l.IrpfWithheld,
            })
            .ToListAsync(ct);

        var baseNominas    = Math.Round(nominas.Sum(n => n.baseRetencion), 2);
        var importeNominas = Math.Round(nominas.Sum(n => n.importeRetencion), 2);

        return Ok(new
        {
            year, quarter,
            period      = $"T{quarter} {year} ({from:dd/MM/yyyy} – {to.AddDays(-1):dd/MM/yyyy})",
            nif         = company?.TaxId,
            razonSocial = company?.Name,
            actividadesProfesionales = new
            {
                casillaNumPerceptores  = "07",
                numPerceptores         = invoices.Select(i => i.clienteNif).Distinct().Count(),
                casillaBase            = "08",
                baseRetencion          = baseTotal,
                casillaImporte         = "09",
                importeRetencion       = importeTotal,
            },
            retencionesNominas = new
            {
                nota = "Retenciones IRPF practicadas a trabajadores (liquidaciones de nómina finalizadas en el trimestre). Ver casillas del modelo 111 para trabajadores en la guía AEAT.",
                numTrabajadores = nominas.Select(n => n.nif).Distinct().Count(),
                baseTotal       = baseNominas,
                retencionTotal  = importeNominas,
                lineas          = nominas,
            },
            totalAIngresar = new
            {
                casilla = "28",
                valor   = Math.Round(importeTotal + importeNominas, 2),
                nota    = "Suma orientativa profesionales + nóminas; validar con asesoría según casillas aplicables.",
            },
            facturasProfesionales = invoices,
        });
    }

    /// <summary>
    /// Libro registro facturas emitidas (campos habituales RIVA Art. 63) — CSV para archivo y revisión.
    /// </summary>
    [HttpGet("libro-iva-emitidas")]
    public async Task<IActionResult> ExportLibroIvaEmitidas([FromQuery] int year, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");
        var from = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to   = from.AddYears(1);

        var list = await _billing.Invoices
            .Include(i => i.InvoiceLines)
            .Where(i => i.CompanyId == tenantId && i.IsLocked && i.IssueDate >= from && i.IssueDate < to)
            .OrderBy(i => i.IssueDate).ThenBy(i => i.Number)
            .AsNoTracking()
            .ToListAsync(ct);

        var rectIds = list.Where(i => i.RectifiedInvoiceId.HasValue)
            .Select(i => i.RectifiedInvoiceId!.Value).Distinct().ToList();
        var rectMap = rectIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _billing.Invoices.AsNoTracking()
                .Where(x => rectIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Number, ct);

        var sb = new StringBuilder();
        sb.AppendLine("Libro facturas emitidas;RIVA Art.63 campos principales;Ejercicio;" + year);
        sb.AppendLine("FechaExpedicion;NumeroFactura;Serie;NIFCliente;NombreCliente;TipoDoc;RectificaNumero;CausaRectificacion;BaseImponibleTotal;CuotaIVATotal;CuotaRE;RetencionIRPF;ImporteTotal;Exenta;Intracomunitaria;Exportacion");
        foreach (var i in list)
        {
            var intra = i.InvoiceLines.Any(l => l.TipoOperacion == "IntraComunitario");
            var exp   = i.InvoiceLines.Any(l => l.TipoOperacion == "Exportacion");
            var rectNum = i.RectifiedInvoiceId.HasValue && rectMap.TryGetValue(i.RectifiedInvoiceId.Value, out var rn)
                ? rn
                : string.Empty;
            sb.AppendLine(string.Join(";",
                i.IssueDate.ToString("dd/MM/yyyy", Es),
                i.Number,
                i.Series,
                i.ClientNif ?? "",
                (i.ClientName ?? "").Replace(";", " "),
                i.InvoiceType,
                rectNum ?? "",
                i.RectificationReasonCode ?? "",
                i.Subtotal.ToString("F2", Es),
                i.TaxAmount.ToString("F2", Es),
                i.SurchargeAmount.ToString("F2", Es),
                i.IrpfAmount.ToString("F2", Es),
                i.Total.ToString("F2", Es),
                i.TaxAmount == 0 && !intra ? "S" : "N",
                intra ? "S" : "N",
                exp ? "S" : "N"));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return FileFiscal(this, bytes, "text/csv", $"LibroIVA_Emitidas_{year}.csv",
            "CSV libro IVA emitidas orientativo; contrastar con normativa RIVA vigente.");
    }

    /// <summary>
    /// Libro registro facturas recibidas (gastos aprobados + IVA soportado) — CSV Art. 64 RIVA.
    /// </summary>
    [HttpGet("libro-iva-recibidas")]
    public async Task<IActionResult> ExportLibroIvaRecibidas([FromQuery] int year, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");
        var from = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to   = from.AddYears(1);

        var docs = await _expenses.ExpenseDocuments
            .Where(e => e.CompanyId == tenantId && e.Status == "Approved"
                     && e.IssueDate >= from && e.IssueDate < to)
            .OrderBy(e => e.IssueDate)
            .AsNoTracking()
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("Libro facturas recibidas;RIVA Art.64;Ejercicio;" + year);
        sb.AppendLine("FechaExpedicion;FechaRegistro;NumeroDocumento;NIFProveedor;NombreProveedor;BaseImponible;CuotaIVASoportada;IRPF;Total;Deducible");
        foreach (var e in docs)
        {
            var fd = e.IssueDate ?? DateTime.UtcNow;
            var baseImp = e.TaxBase ?? e.Total ?? 0m;
            var irpf = e.IRPFAmount ?? 0m;
            sb.AppendLine(string.Join(";",
                fd.ToString("dd/MM/yyyy", Es),
                fd.ToString("dd/MM/yyyy", Es),
                (e.InvoiceNumber ?? e.Id.ToString()[..8]).Replace(";", " "),
                e.SupplierTaxId ?? "",
                (e.SupplierName ?? "").Replace(";", " "),
                baseImp.ToString("F2", Es),
                (e.VATAmount ?? 0m).ToString("F2", Es),
                irpf.ToString("F2", Es),
                ((e.Total ?? 0m) + (e.VATAmount ?? 0m)).ToString("F2", Es),
                (e.VATAmount ?? 0m) > 0 ? "S" : "N"));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return FileFiscal(this, bytes, "text/csv", $"LibroIVA_Recibidas_{year}.csv",
            "CSV libro IVA recibidas orientativo; contrastar con normativa RIVA vigente.");
    }

    // ─── Modelo 347 — Operaciones con terceros (≥ 3.005,06 €) ──────────────────

    /// <summary>
    /// GET /api/accounting/export/modelo347?year=2026
    /// Modelo 347: Declaración anual de operaciones con terceros (art. 29 RD 1065/2007).
    /// Umbral: operaciones ≥ 3.005,06 € (IVA incluido) por NIF de contraparte.
    /// Agrupación por NIF (no por ClientId) para incluir operaciones manuales y NIF extranjeros.
    /// </summary>
    [HttpGet("modelo347")]
    public async Task<IActionResult> ExportModelo347([FromQuery] int year, CancellationToken ct)
    {
        // Umbral fiscal: operaciones con IVA incluido
        const decimal Threshold347 = 3005.06m;

        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var company = await _app.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == tenantId, ct);
        var from = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to   = from.AddYears(1);

        // ── Ventas: agrupar por ClientNif (no ClientId) para captar tanto clientes
        //    registrados como manuales. Incluir todas las facturas bloqueadas del año.
        var invoiceRows = await _billing.Invoices
            .Include(i => i.InvoiceLines)
            .Where(i => i.CompanyId == tenantId && i.IsLocked
                     && i.IssueDate >= from && i.IssueDate < to
                     && !string.IsNullOrWhiteSpace(i.ClientNif))
            .AsNoTracking()
            .ToListAsync(ct);

        // Agrupar por NIF de cliente: base imponible + cuota IVA + cuota IRPF
        var ventasPorNif = invoiceRows
            .GroupBy(i => new { i.ClientNif, i.ClientName })
            .Select(g => new
            {
                Nif            = g.Key.ClientNif,
                Nombre         = g.Key.ClientName ?? string.Empty,
                // Importe total con IVA (umbral se aplica sobre importe total IVA incluido)
                ImporteTotal   = Math.Round(g.Sum(i =>
                    i.InvoiceLines.Sum(l => l.LineTotal + l.TaxAmount + l.SurchargeAmount)), 2),
                BaseImponible  = Math.Round(g.Sum(i => i.Subtotal), 2),
                CuotaIVA       = Math.Round(g.Sum(i => i.TaxAmount + i.SurchargeAmount), 2),
                CuotaIRPF      = Math.Round(g.Sum(i => i.IrpfAmount), 2),
                NumOperaciones = g.Count(),
                EsPersonaFisica = EsPersonaFisica(g.Key.ClientNif ?? string.Empty)
            })
            .Where(r => r.ImporteTotal >= Threshold347)
            // Excluir operaciones con uno mismo (autofacturas, NIF propio)
            .Where(r => !string.Equals(r.Nif?.Trim(), company?.TaxId?.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.ImporteTotal)
            .ToList();

        // ── Compras: gastos aprobados agrupados por SupplierTaxId
        var compraRows = await _expenses.ExpenseDocuments
            .Where(e => e.CompanyId == tenantId && e.Status == "Approved"
                     && e.IssueDate >= from && e.IssueDate < to
                     && !string.IsNullOrWhiteSpace(e.SupplierTaxId))
            .AsNoTracking()
            .ToListAsync(ct);

        var comprasPorNif = compraRows
            .GroupBy(e => new { e.SupplierTaxId, e.SupplierName })
            .Select(g => new
            {
                Nif            = g.Key.SupplierTaxId,
                Nombre         = g.Key.SupplierName ?? string.Empty,
                // Umbral 347 se aplica sobre el total con IVA incluido
                ImporteTotal   = Math.Round(g.Sum(e => (decimal?)((e.VATAmount ?? 0m) > 0 ? (e.Total ?? 0m) + (e.VATAmount ?? 0m) : (e.Total ?? 0m)) ?? 0m), 2),
                BaseImponible  = Math.Round(g.Sum(e => (decimal?)e.Total ?? 0m), 2),
                CuotaIVA       = Math.Round(g.Sum(e => (decimal?)e.VATAmount ?? 0m), 2),
                CuotaIRPF      = 0m,
                NumOperaciones = g.Count(),
                EsPersonaFisica = EsPersonaFisica(g.Key.SupplierTaxId ?? string.Empty)
            })
            .Where(r => r.ImporteTotal >= Threshold347)
            // Excluir operaciones con uno mismo
            .Where(r => !string.Equals(r.Nif?.Trim(), company?.TaxId?.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.ImporteTotal)
            .ToList();

        // ── Generate CSV ───────────────────────────────────────────────────────────
        var sb = new StringBuilder();
        sb.AppendLine("AVISO;Este CSV no sustituye la presentación oficial del modelo 347 sin validación en programa de ayuda AEAT o asesoría.");
        sb.AppendLine($"MODELO 347 — Operaciones con terceros. Año {year}");
        sb.AppendLine($"NIF Declarante;{company?.TaxId}");
        sb.AppendLine($"Razón Social;{company?.Name}");
        sb.AppendLine($"Umbral aplicado;3.005,06 € (IVA incluido)");
        sb.AppendLine($"Nº operadores clientes;{ventasPorNif.Count}");
        sb.AppendLine($"Nº operadores proveedores;{comprasPorNif.Count}");
        sb.AppendLine();
        sb.AppendLine("# CLIENTES (ventas emitidas ≥ 3.005,06 € IVA incluido)");
        sb.AppendLine("NIF;Nombre;ImporteTotal;BaseImponible;CuotaIVA;CuotaIRPF;NumOperaciones;TipoNIF");
        foreach (var r in ventasPorNif)
        {
            sb.AppendLine(string.Join(";",
                r.Nif ?? string.Empty,
                r.Nombre.Replace(";", " "),
                r.ImporteTotal.ToString("F2", Es),
                r.BaseImponible.ToString("F2", Es),
                r.CuotaIVA.ToString("F2", Es),
                r.CuotaIRPF.ToString("F2", Es),
                r.NumOperaciones.ToString(),
                r.EsPersonaFisica ? "F" : "J"));
        }

        sb.AppendLine();
        sb.AppendLine("# PROVEEDORES (compras ≥ 3.005,06 € IVA incluido)");
        sb.AppendLine("NIF;Nombre;ImporteTotal;BaseImponible;CuotaIVA;CuotaIRPF;NumOperaciones;TipoNIF");
        foreach (var r in comprasPorNif)
        {
            sb.AppendLine(string.Join(";",
                r.Nif ?? string.Empty,
                r.Nombre.Replace(";", " "),
                r.ImporteTotal.ToString("F2", Es),
                r.BaseImponible.ToString("F2", Es),
                r.CuotaIVA.ToString("F2", Es),
                r.CuotaIRPF.ToString("F2", Es),
                r.NumOperaciones.ToString(),
                r.EsPersonaFisica ? "F" : "J"));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return FileFiscal(this, bytes, "text/csv", $"Modelo347_{year}.csv",
            "Modelo 347 CSV resumen interno; no es el fichero físico del diseño de registro AEAT.");
    }

    /// <summary>
    /// Fichero .txt con mismos datos que el CSV 347 en formato pipe (importación / revisión en software AEAT).
    /// No sustituye el fichero oficial generado por el programa de ayuda sin validación técnica.
    /// </summary>
    [HttpGet("modelo347-aeat-txt")]
    public async Task<IActionResult> ExportModelo347AeatTxt([FromQuery] int year, CancellationToken ct)
    {
        var r = await ExportModelo347(year, ct);
        if (r is not FileContentResult f)
            return r;
        var txt = Encoding.UTF8.GetString(f.FileContents.Skip(3).ToArray()); // quitar BOM si existe
        if (txt.StartsWith('\uFEFF')) txt = txt.TrimStart('\uFEFF');
        var body = "# Modelo 347 — texto plano (ORIENTATIVO)\r\n" +
                   "# NO es el registro físico del diseño de registro AEAT hasta contrastarlo con el programa de ayuda oficial.\r\n" +
                   txt.Replace("\n", "\r\n");
        var bytes = Encoding.UTF8.GetBytes(body);
        return FileFiscal(this, bytes, "text/plain", $"Modelo347_{year}_aeat.txt",
            "TXT 347 orientativo; no sustituye fichero validado por la AEAT.");
    }

    /// <summary>
    /// Modelo 190 — resumen anual retenciones (trabajadores vía nóminas + profesionales vía facturas con IRPF).
    /// </summary>
    [HttpGet("/api/accounting/modelo-190")]
    public async Task<IActionResult> GetModelo190([FromQuery] int year, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");
        var company = await _app.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == tenantId, ct);
        var from = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to   = from.AddYears(1);

        var prof = await _billing.Invoices
            .Where(i => i.CompanyId == tenantId && i.IsLocked && i.IrpfAmount > 0
                     && i.IssueDate >= from && i.IssueDate < to)
            .AsNoTracking()
            .GroupBy(i => new { i.ClientNif, i.ClientName })
            .Select(g => new
            {
                nif         = g.Key.ClientNif,
                nombre      = g.Key.ClientName,
                baseTotal   = g.Sum(i => i.Subtotal),
                retencion   = g.Sum(i => i.IrpfAmount),
            })
            .ToListAsync(ct);

        var trab = await _payroll.PayrollLines
            .Include(l => l.Settlement)
            .Include(l => l.Employee)
            .Where(l => l.Settlement != null && l.Settlement!.Year == year && l.Settlement.Status == "Final")
            .AsNoTracking()
            .GroupBy(l => new { l.Employee!.TaxId, l.Employee.FullName })
            .Select(g => new
            {
                nif         = g.Key.TaxId,
                nombre      = g.Key.FullName,
                baseTotal   = g.Sum(l => l.IrpfBase),
                retencion   = g.Sum(l => l.IrpfWithheld),
            })
            .ToListAsync(ct);

        return Ok(new
        {
            year,
            nif         = company?.TaxId,
            razonSocial = company?.Name,
            nota        = "Datos orientativos para el 190; contrastar con modelo oficial y asesoría.",
            perceptoresProfesionales = prof,
            perceptoresTrabajadores  = trab,
            totalRetencionesProf     = Math.Round(prof.Sum(p => p.retencion), 2),
            totalRetencionesTrab     = Math.Round(trab.Sum(t => t.retencion), 2),
        });
    }

    /// <summary>
    /// Modelo 130 — pagos fraccionados IRPF actividades económicas (estructura JSON para cumplimentar).
    /// </summary>
    [HttpGet("/api/accounting/modelo-130")]
    public Task<IActionResult> GetModelo130([FromQuery] int year, [FromQuery] int quarter, CancellationToken ct)
    {
        _ = ct;
        if (quarter is < 1 or > 4)
            return Task.FromResult<IActionResult>(BadRequest(new { error = "quarter 1–4" }));
        return Task.FromResult<IActionResult>(Ok(new
        {
            year,
            quarter,
            nota = "El 130 requiere ingresos y gastos estimados del trimestre. Conecte con contabilidad analítica o introduzca datos manualmente en la AEAT.",
            casillasOrientativas = new { ingresos = "01", gastos = "02", baseImponible = "03", tipo = "04", cuota = "05" },
        }));
    }

    /// <summary>
    /// Modelo 200 IS — esqueleto XML (declaración anual sociedades); cumplimentar con datos contables reales.
    /// </summary>
    [HttpGet("modelo200-xml")]
    public async Task<IActionResult> ExportModelo200Xml([FromQuery] int year, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");
        var company = await _app.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == tenantId, ct);
        var xml = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("T200",
                new XComment($"Modelo 200 IS — PLANTILLA / ESQUELETO. Ejercicio {year}. No es declaración AEAT válida hasta cumplimentación y validación técnica."),
                new XElement("Ejercicio", year),
                new XElement("NIF", company?.TaxId ?? ""),
                new XElement("RazonSocial", company?.Name ?? ""),
                new XElement("AvisoLegal", "Plantilla generada por ERP: no presentable como modelo 200 oficial sin revisión fiscal y software AEAT homologado."),
                new XElement("Nota", "Completar todas las páginas obligatorias del diseño de registro vigente antes de presentar.")));
        var bytes = Encoding.UTF8.GetBytes(xml.Declaration + "\n" + xml);
        FiscalExportHeaders.MarkAsNonOfficial(Response.Headers,
            "XML modelo 200: plantilla; no declaración IS validada por AEAT.");
        return await Task.FromResult(File(bytes, "application/xml", $"Modelo200_{year}_esqueleto.xml"));
    }

    /// <summary>
    /// Modelo 202 — pagos fraccionados IS (esqueleto XML).
    /// </summary>
    [HttpGet("modelo202-xml")]
    public async Task<IActionResult> ExportModelo202Xml([FromQuery] int year, [FromQuery] int period, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");
        if (period is < 1 or > 12)
            return await Task.FromResult(BadRequest(new { error = "period 1–12 (mes)" }));
        var company = await _app.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == tenantId, ct);
        var xml = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("T202",
                new XComment($"Modelo 202 IS fraccionado — PLANTILLA. Año {year} mes {period}. No es pago fraccionado validado AEAT."),
                new XElement("Ejercicio", year),
                new XElement("Periodo", period),
                new XElement("NIF", company?.TaxId ?? ""),
                new XElement("RazonSocial", company?.Name ?? ""),
                new XElement("AvisoLegal", "Plantilla ERP: contrastar con diseño de registro modelo 202 y asesoría antes de ingreso o presentación."),
                new XElement("Nota", "Importar bases reales desde contabilidad de sociedad.")));
        var bytes = Encoding.UTF8.GetBytes(xml.Declaration + "\n" + xml);
        FiscalExportHeaders.MarkAsNonOfficial(Response.Headers,
            "XML modelo 202: plantilla; no fraccionado IS validado AEAT.");
        return await Task.FromResult(File(bytes, "application/xml", $"Modelo202_{year}_{period:D2}_esqueleto.xml"));
    }

    // ─── Modelo 390 — Resumen Anual IVA (XML oficial AEAT) ─────────────────────

    /// <summary>
    /// GET /api/accounting/export/modelo390-xml?year=2026
    /// Modelo 390: Resumen anual de IVA (consolidación de los 4 modelos 303 trimestrales).
    /// Obligatorio como resumen anual. Estructura AEAT con casillas 01-65.
    /// </summary>
    [HttpGet("modelo390-xml")]
    public async Task<IActionResult> ExportModelo390Xml([FromQuery] int year, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var company = await _app.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == tenantId, ct);

        // Consolidar los 4 trimestres del año
        decimal[,] devengadoPorTasa = new decimal[4, 3]; // [trimestre, 0=base, 1=cuotaIVA, 2=cuotaRecargo]
        decimal[,] deduciblePorT = new decimal[4, 2];    // [trimestre, 0=ivaDeducible, 1=recargoDeducible]
        decimal[] intracom = new decimal[4];
        decimal[] exportac = new decimal[4];
        decimal[,] regularizacion = new decimal[4, 2]; // [trimestre, 0=bienesInversion, 1=otro]

        for (int t = 1; t <= 4; t++)
        {
            var (from, to) = QuarterRange(year, t);

            var invoices = await _billing.Invoices
                .Include(i => i.InvoiceLines)
                .Where(i => i.CompanyId == tenantId && i.IsLocked
                         && i.IssueDate >= from && i.IssueDate < to)
                .AsNoTracking().ToListAsync(ct);

            var allLines = invoices.SelectMany(i => i.InvoiceLines).ToList();

            var byRate = allLines
                .Where(l => l.TipoOperacion == "Nacional")
                .GroupBy(l => l.TaxRate)
                .ToDictionary(g => g.Key,
                    g => (Base: Math.Round(g.Sum(l => l.LineTotal), 2),
                          Cuota: Math.Round(g.Sum(l => l.TaxAmount), 2)));

            var byRecargo = allLines
                .Where(l => l.TipoOperacion == "Nacional" && l.SurchargeRate > 0)
                .GroupBy(l => l.SurchargeRate)
                .ToDictionary(g => g.Key,
                    g => (Base: Math.Round(g.Sum(l => l.LineTotal), 2),
                          Cuota: Math.Round(g.Sum(l => l.SurchargeAmount), 2)));

            foreach (var (rate, vals) in byRate)
                devengadoPorTasa[t - 1, 0] += vals.Base;
            foreach (var (rate, vals) in byRate)
                devengadoPorTasa[t - 1, 1] += vals.Cuota;
            foreach (var (rate, vals) in byRecargo)
                devengadoPorTasa[t - 1, 2] += vals.Cuota;

            intracom[t - 1] = Math.Round(allLines.Where(l => l.TipoOperacion == "IntraComunitario").Sum(l => l.LineTotal), 2);
            exportac[t - 1] = Math.Round(allLines.Where(l => l.TipoOperacion == "Exportacion").Sum(l => l.LineTotal), 2);

            var deducibleLines = await _accounting.JournalEntryLines
                .Include(l => l.JournalEntry)
                .Where(l => l.JournalEntry.CompanyId == tenantId
                         && l.AccountCode.StartsWith("472")
                         && l.JournalEntry.Date >= from && l.JournalEntry.Date < to)
                .AsNoTracking().ToListAsync(ct);

            deduciblePorT[t - 1, 0] = Math.Round(deducibleLines.Sum(l => l.Debit), 2);
        }

        // Totales anuales por tipo de IVA
        decimal totalBase21 = Enumerable.Range(0, 4).Sum(t => devengadoPorTasa[t, 0] * (devengadoPorTasa[t, 1] > 0 && devengadoPorTasa[t, 0] > 0 ? 1 : 0));
        decimal totalCuota21 = Enumerable.Range(0, 4).Sum(t =>
        {
            var inv = _billing.Invoices.Include(i => i.InvoiceLines)
                .Where(i => i.CompanyId == tenantId && i.IsLocked)
                .AsNoTracking().ToList()
                .Where(i => i.InvoiceLines.Any(l => l.TaxRate == 21m && l.TipoOperacion == "Nacional"))
                .ToList();
            if (inv.Count == 0) return 0m;
            var (from, to) = QuarterRange(year, t + 1);
            return inv.Where(i => i.IssueDate >= from && i.IssueDate < to)
                .SelectMany(i => i.InvoiceLines.Where(l => l.TaxRate == 21m && l.TipoOperacion == "Nacional"))
                .Sum(l => l.TaxAmount);
        });

        // Simplificado: calcular totales consolidados desde las facturas completas del año
        var allYearInvoices = await _billing.Invoices
            .Include(i => i.InvoiceLines)
            .Where(i => i.CompanyId == tenantId && i.IsLocked
                     && i.IssueDate >= new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                     && i.IssueDate < new DateTime(year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            .AsNoTracking().ToListAsync(ct);

        var allYearLines = allYearInvoices.SelectMany(i => i.InvoiceLines).ToList();
        var yearByRate = allYearLines.Where(l => l.TipoOperacion == "Nacional")
            .GroupBy(l => l.TaxRate)
            .ToDictionary(g => g.Key,
                g => (Base: Math.Round(g.Sum(l => l.LineTotal), 2),
                      Cuota: Math.Round(g.Sum(l => l.TaxAmount), 2)));
        var yearByRecargo = allYearLines.Where(l => l.TipoOperacion == "Nacional" && l.SurchargeRate > 0)
            .GroupBy(l => l.SurchargeRate)
            .ToDictionary(g => g.Key,
                g => (Base: Math.Round(g.Sum(l => l.LineTotal), 2),
                      Cuota: Math.Round(g.Sum(l => l.SurchargeAmount), 2)));

        var yearIntracom = Math.Round(allYearLines.Where(l => l.TipoOperacion == "IntraComunitario").Sum(l => l.LineTotal), 2);
        var yearExportac = Math.Round(allYearLines.Where(l => l.TipoOperacion == "Exportacion").Sum(l => l.LineTotal), 2);

        var yearDeducible = Math.Round(
            (await _accounting.JournalEntryLines
                .Include(l => l.JournalEntry)
                .Where(l => l.JournalEntry.CompanyId == tenantId
                         && l.AccountCode.StartsWith("472")
                         && l.JournalEntry.Date.Year == year)
                .AsNoTracking().ToListAsync(ct))
            .Sum(l => l.Debit), 2);

        // Totales consolidados
        decimal totalDevengadoAnual = Math.Round(yearByRate.Values.Sum(v => v.Cuota) + yearByRecargo.Values.Sum(v => v.Cuota), 2);
        decimal totalDeducibleAnual = Math.Round(yearDeducible, 2);
        decimal resultadoAnual = totalDevengadoAnual - totalDeducibleAnual;

        string F(decimal d) => d.ToString("F2", Es);

        // ── XML AEAT Modelo 390 ────────────────────────────────────────────────
        var xml = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("T390",
                new XComment($"Modelo 390 Resumen Anual IVA — {year} — Generado {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}"),
                // Identificación
                new XElement("Ejercicio", year.ToString()),
                new XElement("NIF", company?.TaxId ?? string.Empty),
                new XElement("ApellidosNombre", company?.Name ?? string.Empty),
                new XElement("TipoDeclaracion", "N"),

                // Página 1 — IVA Devengado consolidado anual
                new XElement("Pagina1",
                    // Régimen general — IVA devengado anual
                    new XElement("Casilla001", F(yearByRate.TryGetValue(21m, out var b21) ? b21.Base : 0m)),
                    new XElement("Casilla002", F(yearByRate.TryGetValue(21m, out var c21) ? c21.Cuota : 0m)),
                    new XElement("Casilla003", F(yearByRate.TryGetValue(10m, out var b10) ? b10.Base : 0m)),
                    new XElement("Casilla004", F(yearByRate.TryGetValue(10m, out var c10) ? c10.Cuota : 0m)),
                    new XElement("Casilla005", F(yearByRate.TryGetValue(4m, out var b4) ? b4.Base : 0m)),
                    new XElement("Casilla006", F(yearByRate.TryGetValue(4m, out var c4) ? c4.Cuota : 0m)),
                    new XElement("Casilla007", F(yearByRate.TryGetValue(0m, out var b0) ? b0.Base : 0m)),
                    new XElement("Casilla008", F(yearByRate.TryGetValue(0m, out var c0) ? c0.Cuota : 0m)),
                    // Recargo equivalencia anual
                    new XElement("Casilla031", F(yearByRecargo.TryGetValue(5.2m, out var br52) ? br52.Base : 0m)),
                    new XElement("Casilla032", F(yearByRecargo.TryGetValue(5.2m, out var cr52) ? cr52.Cuota : 0m)),
                    new XElement("Casilla033", F(yearByRecargo.TryGetValue(1.4m, out var br14) ? br14.Base : 0m)),
                    new XElement("Casilla034", F(yearByRecargo.TryGetValue(1.4m, out var cr14) ? cr14.Cuota : 0m)),
                    new XElement("Casilla035", F(yearByRecargo.TryGetValue(0.5m, out var br05) ? br05.Base : 0m)),
                    new XElement("Casilla036", F(yearByRecargo.TryGetValue(0.5m, out var cr05) ? cr05.Cuota : 0m)),
                    // Exentas
                    new XElement("Casilla059", F(yearIntracom)),
                    new XElement("Casilla060", F(yearExportac)),
                    // Total IVA devengado (casilla 27)
                    new XElement("Casilla027", F(totalDevengadoAnual))),

                // Página 2 — IVA Deducible consolidado anual
                new XElement("Pagina2",
                    new XElement("Casilla028", F(totalDeducibleAnual)),
                    new XElement("Casilla029", F(totalDeducibleAnual)),
                    new XElement("Casilla045", F(totalDeducibleAnual))),

                // Página 3 — Regularización y resultado
                new XElement("Pagina3",
                    // Regularización bienes inversión (casilla 51-52)
                    new XElement("Casilla051", "0.00"),
                    new XElement("Casilla052", "0.00"),
                    // Total a ingresar / devolver (casilla 65)
                    new XElement("Casilla065", F(Math.Abs(resultadoAnual))),
                    new XElement("TipoResultado",
                        resultadoAnual >= 0 ? "AIngresar" : "ADevolver"),
                    new XElement("Casilla046", F(Math.Abs(resultadoAnual))))));

        var xmlBytes = Encoding.UTF8.GetBytes(xml.Declaration + "\n" + xml.ToString());
        return FileFiscal(this, xmlBytes, "application/xml", $"Modelo390_{year}.xml",
            "XML modelo 390: contrastar XSD y diseño de registro AEAT vigente antes de presentar.");

    }

    /// <summary>
    /// GET /api/accounting/export/modelo390?year=2026
    /// Modelo 390 en formato CSV de consulta (resumen anual IVA).
    /// </summary>
    [HttpGet("modelo390")]
    public async Task<IActionResult> ExportModelo390([FromQuery] int year, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var company = await _app.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == tenantId, ct);

        var allYearInvoices = await _billing.Invoices
            .Include(i => i.InvoiceLines)
            .Where(i => i.CompanyId == tenantId && i.IsLocked
                     && i.IssueDate >= new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                     && i.IssueDate < new DateTime(year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            .AsNoTracking().ToListAsync(ct);

        var allLines = allYearInvoices.SelectMany(i => i.InvoiceLines).ToList();
        var yearByRate = allLines.Where(l => l.TipoOperacion == "Nacional")
            .GroupBy(l => l.TaxRate)
            .ToDictionary(g => g.Key,
                g => (Base: Math.Round(g.Sum(l => l.LineTotal), 2),
                      Cuota: Math.Round(g.Sum(l => l.TaxAmount), 2)));
        var yearByRecargo = allLines.Where(l => l.TipoOperacion == "Nacional" && l.SurchargeRate > 0)
            .GroupBy(l => l.SurchargeRate)
            .ToDictionary(g => g.Key,
                g => (Base: Math.Round(g.Sum(l => l.LineTotal), 2),
                      Cuota: Math.Round(g.Sum(l => l.SurchargeAmount), 2)));

        var yearIntracom = Math.Round(allLines.Where(l => l.TipoOperacion == "IntraComunitario").Sum(l => l.LineTotal), 2);
        var yearExportac = Math.Round(allLines.Where(l => l.TipoOperacion == "Exportacion").Sum(l => l.LineTotal), 2);

        var yearDeducible = Math.Round(
            (await _accounting.JournalEntryLines
                .Include(l => l.JournalEntry)
                .Where(l => l.JournalEntry.CompanyId == tenantId
                         && l.AccountCode.StartsWith("472")
                         && l.JournalEntry.Date.Year == year)
                .AsNoTracking().ToListAsync(ct))
            .Sum(l => l.Debit), 2);

        var totalDevengado = Math.Round(yearByRate.Values.Sum(v => v.Cuota) + yearByRecargo.Values.Sum(v => v.Cuota), 2);
        var resultado = totalDevengado - yearDeducible;

        var sb = new StringBuilder();
        sb.AppendLine($"MODELO 390 — Resumen Anual IVA. Ejercicio {year}");
        sb.AppendLine($"NIF;{company?.TaxId}"); sb.AppendLine($"Razón Social;{company?.Name}");

        sb.AppendLine(); sb.AppendLine("IVA DEVENGADO — NACIONAL");
        sb.AppendLine("Tipo;BaseImponible;CuotaIVA");
        foreach (var (rate, vals) in yearByRate.OrderByDescending(r => r.Key))
            sb.AppendLine($"Nacional {rate:F0}%;{vals.Base.ToString("F2", Es)};{vals.Cuota.ToString("F2", Es)}");

        if (yearByRecargo.Count > 0)
        {
            sb.AppendLine(); sb.AppendLine("RECARGO DE EQUIVALENCIA");
            foreach (var (rate, vals) in yearByRecargo.OrderByDescending(r => r.Key))
                sb.AppendLine($"Recargo {rate:F1}%;{vals.Base.ToString("F2", Es)};{vals.Cuota.ToString("F2", Es)}");
        }

        if (yearIntracom > 0 || yearExportac > 0)
        {
            sb.AppendLine(); sb.AppendLine("EXENTAS");
            if (yearIntracom > 0) sb.AppendLine($"Intracomunitarias (casilla 59);{yearIntracom.ToString("F2", Es)}");
            if (yearExportac > 0) sb.AppendLine($"Exportaciones (casilla 60);{yearExportac.ToString("F2", Es)}");
        }

        sb.AppendLine(); sb.AppendLine($"Total IVA Devengado (casilla 27);{totalDevengado.ToString("F2", Es)}");
        sb.AppendLine(); sb.AppendLine($"IVA Deducible (casilla 45);{yearDeducible.ToString("F2", Es)}");
        sb.AppendLine(); sb.AppendLine($"RESULTADO ({(resultado >= 0 ? "A INGRESAR" : "A DEVOLVER")}) (casilla 65);{Math.Abs(resultado).ToString("F2", Es)}");

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return FileFiscal(this, bytes, "text/csv", $"Modelo390_{year}.csv",
            "CSV resumen 390 orientativo; validar contra modelo oficial AEAT.");

    }

    // ─── Modelo 303 — XML oficial AEAT ────────────────────────────────────────

    /// <summary>
    /// GET /api/accounting/export/modelo303-xml?year=2026&amp;q=1
    /// Genera el XML del Modelo 303 en el formato estándar AEAT para presentación telemática.
    /// Estructura compatible con el esquema de la AEAT (casillas Modelo 303 v20+).
    /// </summary>
    [HttpGet("modelo303-xml")]
    public async Task<IActionResult> ExportModelo303Xml([FromQuery] int year, [FromQuery] int q, CancellationToken ct)
    {
        if (q < 1 || q > 4)
            return BadRequest(new { error = "q must be 1–4 (quarter)" });

        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var company = await _app.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == tenantId, ct);
        var (from, to) = QuarterRange(year, q);

        var invoices = await _billing.Invoices
            .Include(i => i.InvoiceLines)
            .Where(i => i.CompanyId == tenantId && i.IsLocked
                     && i.IssueDate >= from && i.IssueDate < to)
            .AsNoTracking().ToListAsync(ct);

        var allLines = invoices.SelectMany(i => i.InvoiceLines).ToList();

        // IVA devengado nacional por tipo
        var byRate = allLines
            .Where(l => l.TipoOperacion == "Nacional")
            .GroupBy(l => l.TaxRate)
            .ToDictionary(g => g.Key,
                g => (Base: g.Sum(l => l.LineTotal), Cuota: g.Sum(l => l.TaxAmount)));

        // Recargo de equivalencia
        var byRecargo = allLines
            .Where(l => l.TipoOperacion == "Nacional" && l.SurchargeRate > 0)
            .GroupBy(l => l.SurchargeRate)
            .ToDictionary(g => g.Key,
                g => (Base: g.Sum(l => l.LineTotal), Cuota: g.Sum(l => l.SurchargeAmount)));

        var intracom     = Math.Round(allLines.Where(l => l.TipoOperacion == "IntraComunitario").Sum(l => l.LineTotal), 2);
        var exportac     = Math.Round(allLines.Where(l => l.TipoOperacion == "Exportacion").Sum(l => l.LineTotal), 2);

        // IVA deducible (cuenta 472 del diario)
        var deducible = Math.Round(
            (await _accounting.JournalEntryLines
                .Include(l => l.JournalEntry)
                .Where(l => l.JournalEntry.CompanyId == tenantId
                         && l.AccountCode.StartsWith("472")
                         && l.JournalEntry.Date >= from && l.JournalEntry.Date < to)
                .AsNoTracking().ToListAsync(ct))
            .Sum(l => l.Debit), 2);

        // Totales
        var totalDev = Math.Round(byRate.Values.Sum(v => v.Cuota) + byRecargo.Values.Sum(v => v.Cuota), 2);
        var resultado = totalDev - deducible;
        string F(decimal d) => d.ToString("F2", Es);
        string periodo = q switch { 1 => "1T", 2 => "2T", 3 => "3T", _ => "4T" };

        // ── Casilla helpers ────────────────────────────────────────────────────
        decimal BaseRate(decimal r)  => byRate.TryGetValue(r, out var v) ? Math.Round(v.Base, 2)  : 0m;
        decimal CuotaRate(decimal r) => byRate.TryGetValue(r, out var v) ? Math.Round(v.Cuota, 2) : 0m;
        decimal BaseRec(decimal r)   => byRecargo.TryGetValue(r, out var v) ? Math.Round(v.Base, 2)  : 0m;
        decimal CuotaRec(decimal r)  => byRecargo.TryGetValue(r, out var v) ? Math.Round(v.Cuota, 2) : 0m;

        // ── XML AEAT Modelo 303 ────────────────────────────────────────────────
        var ns = XNamespace.None; // Sin namespace (formato plano AEAT)
        var xml = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("T303",
                new XComment($"Modelo 303 IVA — {year} {periodo} — Generado {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}"),
                // Identificación
                new XElement("Ejercicio",     year.ToString()),
                new XElement("Periodo",       periodo),
                new XElement("NIF",           company?.TaxId  ?? string.Empty),
                new XElement("Apellidos",     company?.Name   ?? string.Empty),
                new XElement("Nombre",        string.Empty),
                new XElement("TipoDeclaracion", "N"), // N=Normal, C=Complementaria, S=Sustitutiva

                // Página 1 — IVA Devengado
                new XElement("Pagina1",
                    // Régimen general — IVA devengado (casillas 01-12)
                    new XElement("Casilla001", F(BaseRate(21m))),
                    new XElement("Casilla002", F(CuotaRate(21m))),
                    new XElement("Casilla003", F(BaseRate(10m))),
                    new XElement("Casilla004", F(CuotaRate(10m))), // spec usa 03/04
                    new XElement("Casilla005", F(BaseRate(4m))),
                    new XElement("Casilla006", F(CuotaRate(4m))),
                    // IVA 0% / Exento sujeto
                    new XElement("Casilla007", F(BaseRate(0m))),
                    new XElement("Casilla008", F(CuotaRate(0m))),
                    // Recargo de equivalencia (casillas 31-36)
                    new XElement("Casilla031", F(BaseRec(5.2m))),
                    new XElement("Casilla032", F(CuotaRec(5.2m))),
                    new XElement("Casilla033", F(BaseRec(1.4m))),
                    new XElement("Casilla034", F(CuotaRec(1.4m))),
                    new XElement("Casilla035", F(BaseRec(0.5m))),
                    new XElement("Casilla036", F(CuotaRec(0.5m))),
                    // Operaciones intracomunitarias / exportaciones (casillas 59-60)
                    new XElement("Casilla059", F(intracom)),
                    new XElement("Casilla060", F(exportac)),
                    // Total IVA devengado (casilla 27)
                    new XElement("Casilla027", F(totalDev))),

                // Página 2 — IVA Deducible
                new XElement("Pagina2",
                    // Operaciones corrientes (casillas 28-29)
                    new XElement("Casilla028", F(deducible)),
                    new XElement("Casilla029", F(deducible)),
                    // Total IVA deducible (casilla 45)
                    new XElement("Casilla045", F(deducible))),

                // Página 3 — Liquidación
                new XElement("Pagina3",
                    // Resultado (casilla 46)
                    new XElement("Casilla046", F(Math.Abs(resultado))),
                    new XElement("TipoResultado",
                        resultado >= 0 ? "AIngresar" : "ACompensar"),
                    // Si es "A ingresar": banco y cuenta IBAN del declarante
                    new XElement("Casilla067", F(resultado >= 0 ? resultado : 0m)))));

        var xmlBytes = Encoding.UTF8.GetBytes(xml.Declaration + "\n" + xml.ToString());
        return FileFiscal(this, xmlBytes, "application/xml", $"Modelo303_{year}_{periodo}.xml",
            "XML 303: contrastar con especificación AEAT vigente antes de presentar.");

    }

    // ─── Modelo 349 — Operaciones intracomunitarias ────────────────────────────

    /// <summary>
    /// GET /api/accounting/export/modelo349?year=2026&amp;q=1
    /// Modelo 349: Declaración recapitulativa de operaciones intracomunitarias (art. 164 LIVA).
    /// Obligatorio para empresas con operaciones con clientes/proveedores de la UE.
    /// Periodicidad: trimestral (mensual si acumulado > 50.000 € en el trimestre).
    /// </summary>
    [HttpGet("modelo349")]
    public async Task<IActionResult> ExportModelo349(
        [FromQuery] int year, [FromQuery] int q, CancellationToken ct)
    {
        if (q < 1 || q > 4)
            return BadRequest(new { error = "q must be 1–4 (quarter)" });

        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var company  = await _app.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == tenantId, ct);
        var (from, to) = QuarterRange(year, q);

        // Entregas intracomunitarias (ventas a clientes UE — tipo E)
        var invoices = await _billing.Invoices
            .Include(i => i.InvoiceLines)
            .Where(i => i.CompanyId == tenantId && i.IsLocked
                     && i.IssueDate >= from && i.IssueDate < to)
            .AsNoTracking().ToListAsync(ct);

        var entregasRows = invoices
            .Where(i => i.InvoiceLines.Any(l => l.TipoOperacion == "IntraComunitario"))
            .GroupBy(i => new { i.ClientNif, i.ClientName })
            .Select(g => new
            {
                CountryCode   = GetEuCountryCode(g.Key.ClientNif),
                NifOp         = StripCountryPrefix(g.Key.ClientNif),
                NombreOp      = g.Key.ClientName ?? string.Empty,
                ClaveOp       = "E",  // E=entregas bienes, S=prestaciones servicios
                BaseImponible = Math.Round(g.Sum(i =>
                    i.InvoiceLines.Where(l => l.TipoOperacion == "IntraComunitario")
                                  .Sum(l => l.LineTotal)), 2),
                NumOperaciones = g.Count()
            })
            .Where(r => r.BaseImponible > 0)
            .ToList();

        // Adquisiciones intracomunitarias (compras a proveedores UE — tipo A).
        // Se detecta el país a partir del prefijo del NIF del proveedor (p.ej. "FR12345" → FR).
        // ExpenseDocument no tiene campo SupplierCountry; el prefijo del VAT number es suficiente.
        var expenseDocs = await _expenses.ExpenseDocuments
            .Where(e => e.CompanyId == tenantId
                     && e.Status == "Approved"
                     && e.IssueDate >= from && e.IssueDate < to
                     && e.SupplierTaxId != null)
            .Select(e => new { e.SupplierTaxId, e.SupplierName, e.Total })
            .AsNoTracking().ToListAsync(ct);

        // Filtrar solo proveedores con VAT number europeo (prefijo 2 letras ≠ "ES")
        var adquisRows = expenseDocs
            .GroupBy(e => new { e.SupplierTaxId, e.SupplierName })
            .Select(g => new
            {
                CountryCode    = GetEuCountryCode(g.Key.SupplierTaxId),
                NifOp          = StripCountryPrefix(g.Key.SupplierTaxId),
                NombreOp       = g.Key.SupplierName ?? string.Empty,
                ClaveOp        = "A",
                BaseImponible  = Math.Round(g.Sum(e => (decimal?)e.Total ?? 0m), 2),
                NumOperaciones = g.Count()
            })
            .Where(r => r.CountryCode != "ES" && r.CountryCode != "ZZ"
                     && r.BaseImponible > 0)
            .ToList();

        var allRows = entregasRows
            .Concat(adquisRows.Select(r => new
            {
                r.CountryCode, r.NifOp, r.NombreOp, r.ClaveOp,
                r.BaseImponible, r.NumOperaciones
            }))
            .OrderByDescending(r => r.BaseImponible)
            .ToList();

        string periodo = q switch { 1 => "1T", 2 => "2T", 3 => "3T", _ => "4T" };
        int totalOps   = allRows.Sum(r => r.NumOperaciones);
        var totalBase  = Math.Round(allRows.Sum(r => r.BaseImponible), 2);

        // ── XML AEAT Modelo 349 ────────────────────────────────────────────────
        var xml = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("T349",
                new XComment($"Modelo 349 — {year} {periodo} — Generado {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}"),
                // Registro declarante
                new XElement("Declarante",
                    new XElement("NIF",         company?.TaxId ?? string.Empty),
                    new XElement("NombreRazon", company?.Name  ?? string.Empty),
                    new XElement("Ejercicio",   year.ToString()),
                    new XElement("Periodo",     periodo),
                    new XElement("Periodicidad", totalBase > 50000m ? "M" : "T"),
                    new XElement("TipoDeclaracion", "N"),
                    new XElement("NombreContacto",
                        company?.Name ?? string.Empty),
                    new XElement("NumOperaciones",   totalOps.ToString()),
                    new XElement("ImporteTotalOper", totalBase.ToString("F2", Es))),

                // Registros de operadores intracomunitarios
                new XElement("Operadores",
                    allRows.Select(r =>
                        new XElement("Operador",
                            new XElement("CodigoPaisOp",  r.CountryCode.ToUpperInvariant()),
                            new XElement("IdOperador",    r.NifOp       ?? string.Empty),
                            new XElement("NombreOp",      r.NombreOp.Length > 40
                                ? r.NombreOp[..40] : r.NombreOp),
                            new XElement("ClaveOp",       r.ClaveOp),
                            new XElement("BaseImponible", r.BaseImponible.ToString("F2", Es)))))));

        var xmlBytes = Encoding.UTF8.GetBytes(xml.Declaration + "\n" + xml.ToString());

        // Also offer CSV version alongside XML
        if (Request.Query.ContainsKey("format") &&
            Request.Query["format"].ToString().Equals("csv", StringComparison.OrdinalIgnoreCase))
        {
            var sb = new StringBuilder();
            sb.AppendLine($"MODELO 349 — Operaciones intracomunitarias. {year} {periodo}");
            sb.AppendLine($"NIF declarante;{company?.TaxId}");
            sb.AppendLine($"Razón Social;{company?.Name}");
            sb.AppendLine($"Total operaciones;{totalOps}  Total base;{totalBase.ToString("F2", Es)}");
            sb.AppendLine();
            sb.AppendLine("PaisOp;NIF_Op;NombreOp;ClaveOp;BaseImponible;NumOps");
            foreach (var r in allRows)
                sb.AppendLine(string.Join(";",
                    r.CountryCode, r.NifOp ?? string.Empty,
                    r.NombreOp.Replace(";", " "),
                    r.ClaveOp, r.BaseImponible.ToString("F2", Es),
                    r.NumOperaciones.ToString()));
            var csvBytes = Encoding.UTF8.GetPreamble()
                .Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return FileFiscal(this, csvBytes, "text/csv", $"Modelo349_{year}_{periodo}.csv",
                "CSV modelo 349 orientativo; validar con diseño AEAT y asesoría.");
        }

        return FileFiscal(this, xmlBytes, "application/xml", $"Modelo349_{year}_{periodo}.xml",
            "XML modelo 349: contrastar especificación AEAT vigente antes de presentar.");
    }

    // ── helpers intracomunitarios ──────────────────────────────────────────────

    /// <summary>Extrae código de país del VAT number UE (p.ej. "FR12345678" → "FR").</summary>
    private static string GetEuCountryCode(string? vatNumber)
    {
        if (string.IsNullOrWhiteSpace(vatNumber) || vatNumber.Length < 2)
            return "ZZ";
        var prefix = vatNumber[..2].ToUpperInvariant();
        return char.IsLetter(prefix[0]) && char.IsLetter(prefix[1]) ? prefix : "ES";
    }

    /// <summary>Elimina prefijo de país del VAT number UE (p.ej. "FR12345678" → "12345678").</summary>
    private static string? StripCountryPrefix(string? vatNumber)
    {
        if (string.IsNullOrWhiteSpace(vatNumber) || vatNumber.Length < 2) return vatNumber;
        var prefix = vatNumber[..2].ToUpperInvariant();
        return char.IsLetter(prefix[0]) && char.IsLetter(prefix[1])
            ? vatNumber[2..]
            : vatNumber;
    }

    private static (DateTime from, DateTime to) QuarterRange(int year, int q) => q switch
    {
        1 => (new DateTime(year, 1,  1, 0, 0, 0, DateTimeKind.Utc), new DateTime(year, 4,  1, 0, 0, 0, DateTimeKind.Utc)),
        2 => (new DateTime(year, 4,  1, 0, 0, 0, DateTimeKind.Utc), new DateTime(year, 7,  1, 0, 0, 0, DateTimeKind.Utc)),
        3 => (new DateTime(year, 7,  1, 0, 0, 0, DateTimeKind.Utc), new DateTime(year, 10, 1, 0, 0, 0, DateTimeKind.Utc)),
        4 => (new DateTime(year, 10, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
        _ => throw new ArgumentException("Quarter must be 1–4")
    };

    private static (string casBase, string casCuota) RateToCasillas(decimal rate) => rate switch
    {
        21m => ("01", "02"), 10m => ("04", "05"), 4m => ("07", "08"), _ => ("10", "11")
    };

    /// <summary>
    /// Recargo de equivalencia → casillas Modelo 303:
    ///   5.2% (vinculado al 21%) → 31/32
    ///   1.4% (vinculado al 10%) → 33/34
    ///   0.5% (vinculado al  4%) → 35/36
    /// </summary>
    private static (string casBase, string casCuota) SurchargeRateToCasillas(decimal rate) => rate switch
    {
        5.2m  => ("31", "32"),
        1.4m  => ("33", "34"),
        0.5m  => ("35", "36"),
        _     => ("31", "32"),
    };

    /// <summary>
    /// Determina si un NIF español corresponde a una persona física (letra 0-9 después del número).
    /// Los NIF de personas jurídicas empiezan con letra (A-Z). Los de extranjeros tienen número.
    /// </summary>
    private static bool EsPersonaFisica(string nif)
    {
        if (string.IsNullOrWhiteSpace(nif)) return false;
        // NIF español persona física: 8 dígitos + 1 letra (ej. 12345678A)
        // NIF persona jurídica: 1 letra + 7 dígitos + 1 letra (ej. A1234567B)
        // NIF extranjero/residente: números extranjera + clave
        if (nif.Length < 9) return false;
        var numPart = nif[..8];
        var letraFinal = nif[^1].ToString();
        // Si los primeros 8 caracteres son todos dígitos → persona física
        return numPart.All(char.IsDigit) && !char.IsLetter(nif[0]);
    }
}
