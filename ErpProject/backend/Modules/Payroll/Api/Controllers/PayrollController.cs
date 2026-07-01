using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Erp.Application.Common.Interfaces;
using Erp.Infrastructure.Fiscal;
using Erp.Modules.Accounting.Application.Services;
using Erp.Modules.Payroll.Application.Interfaces;
using Erp.Modules.Payroll.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Payroll.Api.Controllers;

/// <summary>Nóminas: trabajadores, liquidaciones mensuales y exportes TGSS (TC1/TC2) en CSV estructurado.</summary>
[ApiController]
[Route("api/payroll")]
[Authorize]
public class PayrollController : ControllerBase
{
    private readonly IPayrollDbContext _ctx;
    private readonly ITenantContext _tenant;
    private readonly AccountingService _accounting;

    public PayrollController(IPayrollDbContext ctx, ITenantContext tenant, AccountingService accounting)
    {
        _ctx = ctx;
        _tenant = tenant;
        _accounting = accounting;
    }

    private Guid TenantId => _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");

    // ── Employees ───────────────────────────────────────────────────────────

    [HttpGet("employees")]
    public async Task<IActionResult> ListEmployees(CancellationToken ct)
    {
        var list = await _ctx.Employees
            .AsNoTracking()
            .Where(e => e.IsActive)
            .OrderBy(e => e.FullName)
            .Select(e => new
            {
                e.Id, e.TaxId, e.FullName, e.SocialSecurityNumber, e.HireDate,
                e.ContractType, e.WeeklyHours, e.IsActive
            })
            .ToListAsync(ct);
        return Ok(list);
    }

    [HttpPost("employees")]
    public async Task<IActionResult> CreateEmployee([FromBody] CreateEmployeeBody body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.TaxId) || string.IsNullOrWhiteSpace(body.FullName))
            return BadRequest(new { error = "TaxId y FullName son obligatorios." });

        var e = new Employee
        {
            Id = Guid.NewGuid(),
            CompanyId = TenantId,
            TaxId = body.TaxId.Trim(),
            FullName = body.FullName.Trim(),
            SocialSecurityNumber = body.SocialSecurityNumber?.Trim(),
            HireDate = body.HireDate ?? DateTime.UtcNow,
            ContractType = body.ContractType ?? "Indefinido",
            WeeklyHours = body.WeeklyHours ?? 40m,
            IsActive = true
        };
        _ctx.Employees.Add(e);
        await _ctx.SaveChangesAsync(ct);
        return Created($"/api/payroll/employees/{e.Id}", new { e.Id });
    }

    // ── Settlements ───────────────────────────────────────────────────────────

    [HttpGet("settlements")]
    public async Task<IActionResult> ListSettlements([FromQuery] int? year, CancellationToken ct)
    {
        var y = year ?? DateTime.UtcNow.Year;
        var list = await _ctx.PayrollSettlements
            .AsNoTracking()
            .Where(s => s.Year == y)
            .OrderByDescending(s => s.Year).ThenByDescending(s => s.Month)
            .Select(s => new
            {
                s.Id, s.Year, s.Month, s.Status, s.JournalEntryId,
                lineCount = s.Lines.Count,
                totalGross = s.Lines.Sum(l => l.GrossSalary),
                totalIrpf = s.Lines.Sum(l => l.IrpfWithheld),
                totalEmployerSs = s.Lines.Sum(l => l.EmployerSocialSecurity)
            })
            .ToListAsync(ct);
        return Ok(list);
    }

    [HttpPost("settlements")]
    public async Task<IActionResult> CreateSettlement([FromBody] CreateSettlementBody body, CancellationToken ct)
    {
        if (body.Month is < 1 or > 12)
            return BadRequest(new { error = "Month debe ser 1–12." });

        var exists = await _ctx.PayrollSettlements
            .AnyAsync(s => s.Year == body.Year && s.Month == body.Month, ct);
        if (exists)
            return Conflict(new { error = "Ya existe liquidación para ese mes." });

        var s = new PayrollSettlement
        {
            Id = Guid.NewGuid(),
            CompanyId = TenantId,
            Year = body.Year,
            Month = body.Month,
            Status = "Draft"
        };
        _ctx.PayrollSettlements.Add(s);
        await _ctx.SaveChangesAsync(ct);
        return Created($"/api/payroll/settlements/{s.Id}", new { s.Id });
    }

    [HttpPost("settlements/{id:guid}/lines")]
    public async Task<IActionResult> AddLine(Guid id, [FromBody] AddPayrollLineBody body, CancellationToken ct)
    {
        var settlement = await _ctx.PayrollSettlements
            .Include(s => s.Lines)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
        if (settlement == null) return NotFound();
        if (settlement.Status != "Draft")
            return BadRequest(new { error = "Solo se pueden añadir líneas en borrador." });

        var emp = await _ctx.Employees.FirstOrDefaultAsync(e => e.Id == body.EmployeeId, ct);
        if (emp == null) return BadRequest(new { error = "Empleado no encontrado." });

        if (settlement.Lines.Any(l => l.EmployeeId == body.EmployeeId))
            return BadRequest(new { error = "El empleado ya tiene línea en esta liquidación." });

        var line = new PayrollLine
        {
            Id = Guid.NewGuid(),
            PayrollSettlementId = settlement.Id,
            EmployeeId = body.EmployeeId,
            GrossSalary = body.GrossSalary,
            CommonContingenciesBase = body.CommonContingenciesBase,
            EmployeeSocialSecurity = body.EmployeeSocialSecurity,
            EmployerSocialSecurity = body.EmployerSocialSecurity,
            IrpfBase = body.IrpfBase,
            IrpfRate = body.IrpfRate,
            IrpfWithheld = body.IrpfWithheld,
            NetPay = body.NetPay
        };
        _ctx.PayrollLines.Add(line);
        await _ctx.SaveChangesAsync(ct);
        return Ok(new { line.Id });
    }

    [HttpPost("settlements/{id:guid}/finalize")]
    public async Task<IActionResult> Finalize(Guid id, CancellationToken ct)
    {
        var settlement = await _ctx.PayrollSettlements
            .Include(s => s.Lines)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
        if (settlement == null) return NotFound();

        if (settlement.Status == "Final" && settlement.JournalEntryId != null)
            return Ok(new { message = "Liquidación ya cerrada.", journalEntryId = settlement.JournalEntryId });

        if (!settlement.Lines.Any())
            return BadRequest(new { error = "Añada al menos una línea antes de finalizar." });

        var gross = settlement.Lines.Sum(l => l.GrossSalary);
        var emprSs = settlement.Lines.Sum(l => l.EmployerSocialSecurity);
        var empSs = settlement.Lines.Sum(l => l.EmployeeSocialSecurity);
        var irpf = settlement.Lines.Sum(l => l.IrpfWithheld);
        var net = settlement.Lines.Sum(l => l.NetPay);
        var accrual = new DateTime(settlement.Year, settlement.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var entry = await _accounting.GenerateEntryFromPayrollSettlement(
            TenantId, settlement.Id, settlement.Year, settlement.Month,
            gross, emprSs, empSs, irpf, net, accrual, ct);

        if (settlement.Status == "Draft")
            settlement.Status = "Final";
        settlement.JournalEntryId = entry.Id;
        await _ctx.SaveChangesAsync(ct);

        return Ok(new { message = "Liquidación cerrada y asiento contable generado.", journalEntryId = entry.Id });
    }

    /// <summary>
    /// TC1 (resumen mensual cotizaciones): CSV con bases y cuotas por trabajador para remisión a asesoría / importación Red.
    /// No sustituye el fichero oficial SILTRA/RED sin validación en plataforma TGSS.
    /// </summary>
    [HttpGet("export/tc1")]
    public async Task<IActionResult> ExportTc1([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        if (month is < 1 or > 12) return BadRequest();

        var lines = await _ctx.PayrollLines
            .Include(l => l.Settlement)
            .Include(l => l.Employee)
            .Where(l => l.Settlement!.Year == year && l.Settlement.Month == month && l.Settlement.Status == "Final")
            .AsNoTracking()
            .OrderBy(l => l.Employee!.TaxId)
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("TC1_RESUMEN_COTIZACION;Documento orientativo para TGSS/asesoría");
        sb.AppendLine($"Periodo;{year}-{month:D2}");
        sb.AppendLine("NIF;Nombre;NAF;BaseCC;CotizacionObrera;CotizacionEmpresa;Bruto");
        var es = CultureInfo.InvariantCulture;
        foreach (var l in lines)
        {
            sb.AppendLine(string.Join(";",
                l.Employee!.TaxId,
                l.Employee.FullName.Replace(";", " "),
                l.Employee.SocialSecurityNumber ?? "",
                l.CommonContingenciesBase.ToString("F2", es),
                l.EmployeeSocialSecurity.ToString("F2", es),
                l.EmployerSocialSecurity.ToString("F2", es),
                l.GrossSalary.ToString("F2", es)));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        FiscalExportHeaders.MarkAsNonOfficial(Response.Headers,
            "CSV TC1: no es el fichero RED/SILTRA oficial TGSS; validar con asesoría o plataforma de cotización.");
        return File(bytes, "text/csv", $"TC1_{year}_{month:D2}.csv");
    }

    /// <summary>TC2: desglose complementario (misma estructura extendida con IRPF retenido).</summary>
    [HttpGet("export/tc2")]
    public async Task<IActionResult> ExportTc2([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        if (month is < 1 or > 12) return BadRequest();

        var lines = await _ctx.PayrollLines
            .Include(l => l.Settlement)
            .Include(l => l.Employee)
            .Where(l => l.Settlement!.Year == year && l.Settlement.Month == month && l.Settlement.Status == "Final")
            .AsNoTracking()
            .OrderBy(l => l.Employee!.TaxId)
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("TC2_RETENCIONES_Y_LIQUIDACION;Documento orientativo");
        sb.AppendLine($"Periodo;{year}-{month:D2}");
        sb.AppendLine("NIF;Nombre;Bruto;BaseIRPF;TipoRetencion;IRPFRetenido;Liquido");
        var es = CultureInfo.InvariantCulture;
        foreach (var l in lines)
        {
            sb.AppendLine(string.Join(";",
                l.Employee!.TaxId,
                l.Employee.FullName.Replace(";", " "),
                l.GrossSalary.ToString("F2", es),
                l.IrpfBase.ToString("F2", es),
                l.IrpfRate.ToString("F2", es),
                l.IrpfWithheld.ToString("F2", es),
                l.NetPay.ToString("F2", es)));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        FiscalExportHeaders.MarkAsNonOfficial(Response.Headers,
            "CSV TC2: no es el XML RED oficial; solo apoyo a remisión o revisión.");
        return File(bytes, "text/csv", $"TC2_{year}_{month:D2}.csv");
    }

    /// <summary>
    /// XML orientativo tipo remisión cotización (no sustituye fichero RED validado por TGSS).
    /// </summary>
    [HttpGet("export/tc-red-orientativo")]
    public async Task<IActionResult> ExportTcRedOrientativo([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        if (month is < 1 or > 12) return BadRequest();

        var lines = await _ctx.PayrollLines
            .Include(l => l.Settlement)
            .Include(l => l.Employee)
            .Where(l => l.Settlement!.Year == year && l.Settlement.Month == month && l.Settlement.Status == "Final")
            .AsNoTracking()
            .OrderBy(l => l.Employee!.TaxId)
            .ToListAsync(ct);

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("RemisionCotizacionOrientativa",
                new XComment("NO es el XML RED oficial de la TGSS. Contrastar con SILTRA/asesoría antes de uso."),
                new XElement("Periodo", $"{year}-{month:D2}"),
                new XElement("Lineas",
                    lines.Select(l => new XElement("Trabajador",
                        new XElement("NIF", l.Employee!.TaxId),
                        new XElement("NAF", l.Employee.SocialSecurityNumber ?? ""),
                        new XElement("Nombre", l.Employee.FullName),
                        new XElement("BaseCC", l.CommonContingenciesBase.ToString("F2", CultureInfo.InvariantCulture)),
                        new XElement("CotizacionObrera", l.EmployeeSocialSecurity.ToString("F2", CultureInfo.InvariantCulture)),
                        new XElement("CotizacionEmpresa", l.EmployerSocialSecurity.ToString("F2", CultureInfo.InvariantCulture)))))));

        var bytes = Encoding.UTF8.GetBytes(doc.Declaration + "\n" + doc);
        FiscalExportHeaders.MarkAsNonOfficial(Response.Headers,
            "XML orientativo: no reemplaza el fichero RED oficial TGSS.");
        return File(bytes, "application/xml", $"TC_RED_orientativo_{year}_{month:D2}.xml");
    }

    public sealed class CreateEmployeeBody
    {
        public string TaxId { get; set; } = "";
        public string FullName { get; set; } = "";
        public string? SocialSecurityNumber { get; set; }
        public DateTime? HireDate { get; set; }
        public string? ContractType { get; set; }
        public decimal? WeeklyHours { get; set; }
    }

    public sealed class CreateSettlementBody
    {
        public int Year { get; set; }
        public int Month { get; set; }
    }

    public sealed class AddPayrollLineBody
    {
        public Guid EmployeeId { get; set; }
        public decimal GrossSalary { get; set; }
        public decimal CommonContingenciesBase { get; set; }
        public decimal EmployeeSocialSecurity { get; set; }
        public decimal EmployerSocialSecurity { get; set; }
        public decimal IrpfBase { get; set; }
        public decimal IrpfRate { get; set; }
        public decimal IrpfWithheld { get; set; }
        public decimal NetPay { get; set; }
    }
}
