using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Erp.Modules.Accounting.Application.Features.Export;
using Erp.Modules.Accounting.Application.Interfaces;

namespace Erp.Modules.Accounting.Infrastructure.Services;

public class Modelo303XmlExporter : IModelo303XmlExporter
{
    private static readonly CultureInfo Es = CultureInfo.InvariantCulture;

    private readonly IModelo303Reader _reader;

    public Modelo303XmlExporter(IModelo303Reader reader) => _reader = reader;

    public async Task<FiscalCsvExportResult> ExportAsync(
        Guid tenantId, int year, int quarter, CancellationToken ct)
    {
        var data = await _reader.GetQuarterAsync(tenantId, year, quarter, ct);
        string F(decimal d) => d.ToString("F2", Es);
        var periodo = quarter switch { 1 => "1T", 2 => "2T", 3 => "3T", _ => "4T" };

        decimal BaseRate(decimal r) => data.Nacional.FirstOrDefault(x => x.Rate == r)?.Base ?? 0m;
        decimal CuotaRate(decimal r) => data.Nacional.FirstOrDefault(x => x.Rate == r)?.Cuota ?? 0m;
        decimal BaseRec(decimal r) => data.Recargo.FirstOrDefault(x => x.Rate == r)?.Base ?? 0m;
        decimal CuotaRec(decimal r) => data.Recargo.FirstOrDefault(x => x.Rate == r)?.Cuota ?? 0m;

        var xml = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("T303",
                new XComment($"Modelo 303 IVA — {year} {periodo} — Generado {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}"),
                new XElement("Ejercicio", year.ToString()),
                new XElement("Periodo", periodo),
                new XElement("NIF", data.Nif ?? string.Empty),
                new XElement("Apellidos", data.RazonSocial ?? string.Empty),
                new XElement("Nombre", string.Empty),
                new XElement("TipoDeclaracion", "N"),
                new XElement("Pagina1",
                    new XElement("Casilla001", F(BaseRate(21m))),
                    new XElement("Casilla002", F(CuotaRate(21m))),
                    new XElement("Casilla003", F(BaseRate(10m))),
                    new XElement("Casilla004", F(CuotaRate(10m))),
                    new XElement("Casilla005", F(BaseRate(4m))),
                    new XElement("Casilla006", F(CuotaRate(4m))),
                    new XElement("Casilla007", F(BaseRate(0m))),
                    new XElement("Casilla008", F(CuotaRate(0m))),
                    new XElement("Casilla031", F(BaseRec(5.2m))),
                    new XElement("Casilla032", F(CuotaRec(5.2m))),
                    new XElement("Casilla033", F(BaseRec(1.4m))),
                    new XElement("Casilla034", F(CuotaRec(1.4m))),
                    new XElement("Casilla035", F(BaseRec(0.5m))),
                    new XElement("Casilla036", F(CuotaRec(0.5m))),
                    new XElement("Casilla059", F(data.Intracom)),
                    new XElement("Casilla060", F(data.Exportaciones)),
                    new XElement("Casilla027", F(data.TotalDevengado))),
                new XElement("Pagina2",
                    new XElement("Casilla028", F(data.IvaDeducible)),
                    new XElement("Casilla029", F(data.IvaDeducible)),
                    new XElement("Casilla045", F(data.IvaDeducible))),
                new XElement("Pagina3",
                    new XElement("Casilla046", F(Math.Abs(data.Resultado))),
                    new XElement("TipoResultado", data.Resultado >= 0 ? "AIngresar" : "ACompensar"),
                    new XElement("Casilla067", F(data.Resultado >= 0 ? data.Resultado : 0m)))));

        var xmlBytes = Encoding.UTF8.GetBytes(xml.Declaration + "\n" + xml);
        return new FiscalCsvExportResult
        {
            Content = xmlBytes,
            ContentType = "application/xml",
            FileName = $"Modelo303_{year}_{periodo}.xml",
            Disclaimer = "XML 303: contrastar con especificación AEAT vigente antes de presentar."
        };
    }
}
