using System.Text;
using System.Xml.Linq;
using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Features.Export;
using Erp.Modules.Accounting.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Infrastructure.Services;

public class FiscalSkeletonXmlExporter : IFiscalSkeletonXmlExporter
{
    private readonly IApplicationDbContext _app;

    public FiscalSkeletonXmlExporter(IApplicationDbContext app) => _app = app;

    public async Task<FiscalCsvExportResult> ExportModelo200Async(Guid tenantId, int year, CancellationToken ct)
    {
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
        return ToResult(xml, $"Modelo200_{year}_esqueleto.xml",
            "XML modelo 200: plantilla; no declaración IS validada por AEAT.");
    }

    public async Task<FiscalCsvExportResult> ExportModelo202Async(
        Guid tenantId, int year, int period, CancellationToken ct)
    {
        if (period is < 1 or > 12)
            throw new ArgumentException("period 1–12 (mes)");

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
        return ToResult(xml, $"Modelo202_{year}_{period:D2}_esqueleto.xml",
            "XML modelo 202: plantilla; no fraccionado IS validado AEAT.");
    }

    private static FiscalCsvExportResult ToResult(XDocument xml, string fileName, string disclaimer)
    {
        var bytes = Encoding.UTF8.GetBytes(xml.Declaration + "\n" + xml);
        return new FiscalCsvExportResult
        {
            Content = bytes,
            ContentType = "application/xml",
            FileName = fileName,
            Disclaimer = disclaimer
        };
    }
}
