using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Erp.Application.Common.Interfaces;
using Erp.Modules.Payroll.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Payroll.Application.Features.Exports;

public record PayrollFiscalExportResult(
    byte[] Content, string ContentType, string FileName, string Disclaimer);

public record ExportTc1Query(int Year, int Month) : IRequest<PayrollFiscalExportResult>;
public record ExportTc2Query(int Year, int Month) : IRequest<PayrollFiscalExportResult>;
public record ExportTcRedOrientativoQuery(int Year, int Month) : IRequest<PayrollFiscalExportResult>;
public record ExportRedQuery(int Year, int Month) : IRequest<PayrollFiscalExportResult>;

internal static class PayrollExportLineLoader
{
    internal static async Task<List<PayrollExportLine>> LoadFinalLinesAsync(
        IPayrollDbContext ctx, int year, int month, CancellationToken ct)
    {
        return await ctx.PayrollLines
            .Include(l => l.Settlement)
            .Include(l => l.Employee)
            .Where(l => l.Settlement!.Year == year && l.Settlement.Month == month && l.Settlement.Status == "Final")
            .AsNoTracking()
            .OrderBy(l => l.Employee!.TaxId)
            .Select(l => new PayrollExportLine(
                l.Employee!.TaxId,
                l.Employee.FullName,
                l.Employee.SocialSecurityNumber,
                l.CommonContingenciesBase,
                l.EmployeeSocialSecurity,
                l.EmployerSocialSecurity,
                l.GrossSalary,
                l.IrpfBase,
                l.IrpfRate,
                l.IrpfWithheld,
                l.NetPay))
            .ToListAsync(ct);
    }
}

internal record PayrollExportLine(
    string TaxId, string FullName, string? SocialSecurityNumber,
    decimal CommonContingenciesBase, decimal EmployeeSocialSecurity,
    decimal EmployerSocialSecurity, decimal GrossSalary,
    decimal IrpfBase, decimal IrpfRate, decimal IrpfWithheld, decimal NetPay);

public class ExportTc1Handler : IRequestHandler<ExportTc1Query, PayrollFiscalExportResult>
{
    private readonly IPayrollDbContext _ctx;
    public ExportTc1Handler(IPayrollDbContext ctx) => _ctx = ctx;

    public async Task<PayrollFiscalExportResult> Handle(ExportTc1Query request, CancellationToken ct)
    {
        PayrollExportValidators.ValidateMonth(request.Month);
        var lines = await PayrollExportLineLoader.LoadFinalLinesAsync(_ctx, request.Year, request.Month, ct);

        var sb = new StringBuilder();
        sb.AppendLine("TC1_RESUMEN_COTIZACION;Documento orientativo para TGSS/asesoría");
        sb.AppendLine($"Periodo;{request.Year}-{request.Month:D2}");
        sb.AppendLine("NIF;Nombre;NAF;BaseCC;CotizacionObrera;CotizacionEmpresa;Bruto");
        var es = CultureInfo.InvariantCulture;
        foreach (var l in lines)
        {
            sb.AppendLine(string.Join(";",
                l.TaxId,
                l.FullName.Replace(";", " "),
                l.SocialSecurityNumber ?? "",
                l.CommonContingenciesBase.ToString("F2", es),
                l.EmployeeSocialSecurity.ToString("F2", es),
                l.EmployerSocialSecurity.ToString("F2", es),
                l.GrossSalary.ToString("F2", es)));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return new PayrollFiscalExportResult(
            bytes, "text/csv", $"TC1_{request.Year}_{request.Month:D2}.csv",
            "CSV TC1: no es el fichero RED/SILTRA oficial TGSS; validar con asesoría o plataforma de cotización.");
    }
}

public class ExportTc2Handler : IRequestHandler<ExportTc2Query, PayrollFiscalExportResult>
{
    private readonly IPayrollDbContext _ctx;
    public ExportTc2Handler(IPayrollDbContext ctx) => _ctx = ctx;

    public async Task<PayrollFiscalExportResult> Handle(ExportTc2Query request, CancellationToken ct)
    {
        PayrollExportValidators.ValidateMonth(request.Month);
        var lines = await PayrollExportLineLoader.LoadFinalLinesAsync(_ctx, request.Year, request.Month, ct);

        var sb = new StringBuilder();
        sb.AppendLine("TC2_RETENCIONES_Y_LIQUIDACION;Documento orientativo");
        sb.AppendLine($"Periodo;{request.Year}-{request.Month:D2}");
        sb.AppendLine("NIF;Nombre;Bruto;BaseIRPF;TipoRetencion;IRPFRetenido;Liquido");
        var es = CultureInfo.InvariantCulture;
        foreach (var l in lines)
        {
            sb.AppendLine(string.Join(";",
                l.TaxId,
                l.FullName.Replace(";", " "),
                l.GrossSalary.ToString("F2", es),
                l.IrpfBase.ToString("F2", es),
                l.IrpfRate.ToString("F2", es),
                l.IrpfWithheld.ToString("F2", es),
                l.NetPay.ToString("F2", es)));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return new PayrollFiscalExportResult(
            bytes, "text/csv", $"TC2_{request.Year}_{request.Month:D2}.csv",
            "CSV TC2: no es el XML RED oficial; solo apoyo a remisión o revisión.");
    }
}

public class ExportTcRedOrientativoHandler : IRequestHandler<ExportTcRedOrientativoQuery, PayrollFiscalExportResult>
{
    private readonly IPayrollDbContext _ctx;
    public ExportTcRedOrientativoHandler(IPayrollDbContext ctx) => _ctx = ctx;

    public async Task<PayrollFiscalExportResult> Handle(ExportTcRedOrientativoQuery request, CancellationToken ct)
    {
        PayrollExportValidators.ValidateMonth(request.Month);
        var lines = await PayrollExportLineLoader.LoadFinalLinesAsync(_ctx, request.Year, request.Month, ct);

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("RemisionCotizacionOrientativa",
                new XComment("NO es el XML RED oficial de la TGSS. Contrastar con SILTRA/asesoría antes de uso."),
                new XElement("Periodo", $"{request.Year}-{request.Month:D2}"),
                new XElement("Lineas",
                    lines.Select(l => new XElement("Trabajador",
                        new XElement("NIF", l.TaxId),
                        new XElement("NAF", l.SocialSecurityNumber ?? ""),
                        new XElement("Nombre", l.FullName),
                        new XElement("BaseCC", l.CommonContingenciesBase.ToString("F2", CultureInfo.InvariantCulture)),
                        new XElement("CotizacionObrera", l.EmployeeSocialSecurity.ToString("F2", CultureInfo.InvariantCulture)),
                        new XElement("CotizacionEmpresa", l.EmployerSocialSecurity.ToString("F2", CultureInfo.InvariantCulture)))))));

        var bytes = Encoding.UTF8.GetBytes(doc.Declaration + "\n" + doc);
        return new PayrollFiscalExportResult(
            bytes, "application/xml", $"TC_RED_orientativo_{request.Year}_{request.Month:D2}.xml",
            "XML orientativo: no reemplaza el fichero RED oficial TGSS.");
    }
}

public class ExportRedHandler : IRequestHandler<ExportRedQuery, PayrollFiscalExportResult>
{
    private readonly IPayrollDbContext _ctx;
    private readonly IApplicationDbContext _app;
    private readonly ITenantContext _tenant;

    public ExportRedHandler(IPayrollDbContext ctx, IApplicationDbContext app, ITenantContext tenant)
    {
        _ctx = ctx;
        _app = app;
        _tenant = tenant;
    }

    public async Task<PayrollFiscalExportResult> Handle(ExportRedQuery request, CancellationToken ct)
    {
        PayrollExportValidators.ValidateMonth(request.Month);
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var company = await _app.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId, ct)
            ?? throw new InvalidOperationException("Empresa no encontrada.");

        var lines = await PayrollExportLineLoader.LoadFinalLinesAsync(_ctx, request.Year, request.Month, ct);
        var bytes = RedSiltraFileBuilder.Build(
            company.TaxId, company.Name, request.Year, request.Month, lines);

        return new PayrollFiscalExportResult(
            bytes,
            "text/plain",
            $"RED_{request.Year}_{request.Month:D2}.txt",
            "Fichero RED longitud fija 250: estructura orientativa SILTRA/TGSS. " +
            "No homologado — validar con asesoría, SILTRA o RED oficial antes de remisión.");
    }
}

internal static class PayrollExportValidators
{
    internal static void ValidateMonth(int month)
    {
        if (month is < 1 or > 12)
            throw new ArgumentException("Month debe ser 1–12.");
    }
}
