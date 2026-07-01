using Erp.Application.Common.Interfaces;
using Erp.Infrastructure.Services;
using Erp.Modules.Billing.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;
using System.Xml;

namespace Erp.Infrastructure.Services.Sii;

/// <summary>
/// Genera el XML de registro VERI*FACTU según RD 1007/2023 y la especificación técnica
/// publicada por la AEAT (esquema SuministroInformacion.xsd).
///
/// Diferencias clave con SII:
///   - Endpoint: /wlpl/TIKE-CONT/ws/SuministroFacturas (TIKE, no SSII-FACT)
///   - Esquema: SuministroLRFacturasEmitidas (no SuministroLRFacturasEmitidas de SII)
///   - Incluye: HuellaRegistro (hash SHA256 Anexo II), FechaHoraHusoGenRegistro,
///              SistemaInformatico (identificación del software emisor)
///   - Sin firma XAdES — la integridad la garantiza el hash chain (Art. 6 RD 1007/2023)
///
/// Config:
///   Verifactu:NifSoftware       — NIF del fabricante del software
///   Verifactu:NombreSoftware    — Nombre del software
///   Verifactu:IdSistema         — Id del sistema (e.g. "ANTIGRAVITY-ERP-1")
///   Verifactu:Version           — Versión del software
///   Verifactu:NumeroInstalacion — Número de instalación (único por cliente)
/// </summary>
public class VerifactuXmlGenerator
{
    private readonly IBillingDbContext _billing;
    private readonly IApplicationDbContext _app;
    private readonly IVerifactuService _verifactu;
    private readonly string _nifSoftware;
    private readonly string _nombreSoftware;
    private readonly string _idSistema;
    private readonly string _version;
    private readonly string _numeroInstalacion;

    private const string Namespace = "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/SuministroInformacion.xsd";

    public VerifactuXmlGenerator(
        IBillingDbContext billing,
        IApplicationDbContext app,
        IVerifactuService verifactu,
        IOptions<VerifactuOptions> options)
    {
        _billing           = billing;
        _app               = app;
        _verifactu         = verifactu;
        var opts = options.Value;
        opts.AssertValid();
        _nifSoftware       = opts.NifSoftware.Trim();
        _nombreSoftware    = opts.NombreSoftware.Trim();
        _idSistema         = opts.IdSistema.Trim();
        _version           = string.IsNullOrWhiteSpace(opts.Version) ? "1.0" : opts.Version.Trim();
        _numeroInstalacion = string.IsNullOrWhiteSpace(opts.NumeroInstalacion) ? "1" : opts.NumeroInstalacion.Trim();
    }

    /// <summary>
    /// Genera el XML VERI*FACTU para las facturas emitidas de una empresa en un periodo.
    /// </summary>
    public async Task<string> GenerateRegistroAsync(
        Guid companyId,
        int year,
        int month,
        CancellationToken ct = default)
    {
        var company = await _app.Companies
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == companyId, ct)
            ?? throw new InvalidOperationException($"Company {companyId} not found.");

        var from = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var to   = from.AddMonths(1);

        var invoices = await _billing.Invoices
            .Include(i => i.InvoiceLines)
            .Where(i => i.CompanyId == companyId
                     && i.IssueDate >= from
                     && i.IssueDate < to
                     && i.Status != "Draft"
                     && i.VerifactuHuella != null
                     && i.VerifactuSubmittedAt == null)
            .OrderBy(i => i.SequenceNumber)
            .ToListAsync(ct);

        var sb  = new StringBuilder();
        var settings = new XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8 };

        using var writer = XmlWriter.Create(sb, settings);
        writer.WriteStartDocument();

        // Root: SuministroLRFacturasEmitidas
        writer.WriteStartElement("sum", "SuministroLRFacturasEmitidas", Namespace);
        writer.WriteAttributeString("xmlns", "sum", null, Namespace);

        // Cabecera
        writer.WriteStartElement("Cabecera");
        writer.WriteElementString("IDVersionSii", "1.1");
        writer.WriteStartElement("Titular");
        writer.WriteElementString("NombreRazon", company.Name);
        writer.WriteElementString("NIF", company.TaxId ?? string.Empty);
        writer.WriteEndElement(); // Titular
        writer.WriteElementString("TipoComunicacion", "A0"); // A0=Alta, A1=Modificación
        writer.WriteEndElement(); // Cabecera

        foreach (var inv in invoices)
        {
            writer.WriteStartElement("RegistroFacturacionAlta");

            // IDFactura
            writer.WriteStartElement("IDFactura");
            writer.WriteStartElement("IDEmisorFactura");
            writer.WriteElementString("NIF", company.TaxId ?? string.Empty);
            writer.WriteEndElement();
            writer.WriteElementString("NumSerieFactura", inv.Number);
            writer.WriteElementString("FechaExpedicionFacturaEmisor",
                inv.IssueDate.ToString("dd-MM-yyyy"));
            writer.WriteEndElement(); // IDFactura

            // NombreRazonEmisor
            writer.WriteElementString("NombreRazonEmisor", company.Name);

            // TipoFactura — F1 normal, R1 rectificativa
            var tipoFactura = inv.InvoiceType == "Rectificativa" ? "R1" : "F1";
            writer.WriteElementString("TipoFactura", tipoFactura);

            // DescripcionOperacion (requerido)
            writer.WriteElementString("DescripcionOperacion",
                $"Factura {inv.Number} — periodo {inv.IssueDate:yyyy-MM}");

            // Desglose IVA
            writer.WriteStartElement("DesgloseFactura");
            writer.WriteStartElement("DesgloseTipoOperacion");
            writer.WriteStartElement("Entrega");
            writer.WriteStartElement("Sujeta");
            writer.WriteStartElement("NoExenta");
            writer.WriteElementString("TipoNoExenta", "S1");
            writer.WriteStartElement("DesgloseIVA");
            writer.WriteStartElement("DetalleIVA");
            writer.WriteElementString("TipoImpositivo",
                inv.InvoiceLines.FirstOrDefault()?.TaxRate.ToString("F2", CultureInfo.InvariantCulture) ?? "21.00");
            writer.WriteElementString("BaseImponible",
                inv.Subtotal.ToString("F2", CultureInfo.InvariantCulture));
            writer.WriteElementString("CuotaRepercutida",
                inv.TaxAmount.ToString("F2", CultureInfo.InvariantCulture));
            writer.WriteEndElement(); // DetalleIVA
            writer.WriteEndElement(); // DesgloseIVA
            writer.WriteEndElement(); // NoExenta
            writer.WriteEndElement(); // Sujeta
            writer.WriteEndElement(); // Entrega
            writer.WriteEndElement(); // DesgloseTipoOperacion
            writer.WriteEndElement(); // DesgloseFactura

            // Cuota y Importe total
            writer.WriteElementString("CuotaTotal",
                inv.TaxAmount.ToString("F2", CultureInfo.InvariantCulture));
            writer.WriteElementString("ImporteTotal",
                inv.Total.ToString("F2", CultureInfo.InvariantCulture));

            // Encadenamiento (hash chain)
            if (!string.IsNullOrEmpty(inv.PreviousHash))
            {
                writer.WriteStartElement("EncadenamientoFacturaAnterior");
                writer.WriteStartElement("IDFacturaAnterior");
                // Nota: en producción se incluyen IDEmisorFacturaAnterior y NumSerieFacturaAnterior
                // Para el hash chain solo es necesario el valor de la Huella anterior
                writer.WriteEndElement();
                writer.WriteElementString("HuellaFacturaAnterior", inv.PreviousHash);
                writer.WriteEndElement(); // EncadenamientoFacturaAnterior
            }

            // Sistema informático (identificación del software — Art. 9 RD 1007/2023)
            writer.WriteStartElement("SistemaInformatico");
            writer.WriteElementString("NombreRazon", _nombreSoftware);
            writer.WriteElementString("NIF", _nifSoftware);
            writer.WriteElementString("IdSistemaInformatico", _idSistema);
            writer.WriteElementString("Version", _version);
            writer.WriteElementString("NumeroInstalacion", _numeroInstalacion);
            writer.WriteElementString("TipoUsoPosibleSoloVerifactu", "S");
            writer.WriteElementString("TipoUsoPosibleMultipleOT", "N");
            writer.WriteElementString("IndicadorMultiplesOT", "N");
            writer.WriteEndElement(); // SistemaInformatico

            // Timestamp de generación del registro
            writer.WriteElementString("FechaHoraHusoGenRegistro",
                (inv.LockedAt ?? inv.IssueDate).ToString("yyyy-MM-ddTHH:mm:sszzz"));

            // Huella (SHA256 Anexo II RD 1007/2023)
            writer.WriteElementString("Huella", inv.VerifactuHuella ?? string.Empty);

            // QR URL para validación AEAT
            writer.WriteElementString("UrlValidacion", inv.VerifactuQrUrl ?? string.Empty);

            writer.WriteEndElement(); // RegistroFacturacionAlta
        }

        writer.WriteEndElement(); // SuministroLRFacturasEmitidas
        writer.WriteEndDocument();
        writer.Flush();

        return sb.ToString();
    }
}
