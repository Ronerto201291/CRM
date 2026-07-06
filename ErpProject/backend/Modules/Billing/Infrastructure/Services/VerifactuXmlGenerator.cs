using Erp.Application.Common;
using Erp.Application.Common.Interfaces;
using Erp.Infrastructure.Services;
using Erp.Modules.Billing.Application.Interfaces;
using Erp.Modules.Billing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;
using System.Xml;

namespace Erp.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Genera XML VERI*FACTU (RD 1007/2023) conforme esquema tikeV1.0 AEAT.
/// </summary>
public class VerifactuXmlGenerator : IVerifactuXmlGenerator
{
    private const string NsInfo = "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tikeV1.0/cont/ws/SuministroInformacion.xsd";
    private const string NsLR   = "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tikeV1.0/cont/ws/SuministroLR.xsd";

    private readonly IBillingDbContext _billing;
    private readonly IApplicationDbContext _app;
    private readonly string _nifSoftware;
    private readonly string _nombreSoftware;
    private readonly string _idSistema;
    private readonly string _version;
    private readonly string _numeroInstalacion;
    private readonly bool _realtimeSubmission;

    public VerifactuXmlGenerator(
        IBillingDbContext billing,
        IApplicationDbContext app,
        IVerifactuService verifactu,
        IOptions<VerifactuOptions> options)
    {
        _ = verifactu;
        _billing = billing;
        _app = app;
        var opts = options.Value;
        opts.AssertValid();
        _nifSoftware       = opts.NifSoftware.Trim();
        _nombreSoftware    = opts.NombreSoftware.Trim();
        _idSistema         = opts.IdSistema.Trim();
        _version           = string.IsNullOrWhiteSpace(opts.Version) ? "1.0" : opts.Version.Trim();
        _numeroInstalacion = string.IsNullOrWhiteSpace(opts.NumeroInstalacion) ? "1" : opts.NumeroInstalacion.Trim();
        _realtimeSubmission = opts.SubmissionMode != VerifactuSubmissionMode.LocalOnly;
    }

    public Task<string> GenerateRegistroAsync(
        Guid companyId, int year, int month, CancellationToken ct = default)
        => GenerateAsync(companyId, year, month, invoiceId: null, ct);

    public async Task<string> GenerateSingleInvoiceRegistroAsync(
        Guid invoiceId, CancellationToken ct = default)
    {
        var inv = await _billing.Invoices
            .Include(i => i.InvoiceLines)
            .Include(i => i.RectifiedInvoice)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice {invoiceId} not found.");

        return await GenerateAsync(inv.CompanyId, inv.IssueDate.Year, inv.IssueDate.Month, invoiceId, ct);
    }

    public async Task<string> GenerateAnulacionRegistroAsync(Guid invoiceId, CancellationToken ct = default)
    {
        var inv = await _billing.Invoices
            .Include(i => i.InvoiceLines)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct)
            ?? throw new InvalidOperationException($"Invoice {invoiceId} not found.");

        if (string.IsNullOrEmpty(inv.VerifactuAnulacionHuella))
            throw new InvalidOperationException("La factura no tiene registro de anulación VeriFactu.");

        var company = await _app.Companies
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == inv.CompanyId, ct)
            ?? throw new InvalidOperationException($"Company {inv.CompanyId} not found.");

        var previous = await GetPreviousRegistroAsync(
            inv.CompanyId, inv.Series, inv.FiscalYear, inv.VerifactuAnulacionAt ?? DateTime.UtcNow, ct);

        var sb = new StringBuilder();
        using var writer = XmlWriter.Create(sb, new XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8 });
        writer.WriteStartDocument();
        writer.WriteStartElement("sfLR", "RegFactuSistemaFacturacion", NsLR);
        writer.WriteAttributeString("xmlns", "sfLR", null, NsLR);
        writer.WriteAttributeString("xmlns", "sf", null, NsInfo);
        WriteCabecera(writer, company.Name, company.TaxId ?? string.Empty);
        writer.WriteStartElement("sfLR", "RegistroFactura", NsLR);
        writer.WriteStartElement("sf", "RegistroAnulacion", NsInfo);
        writer.WriteElementString("sf", "IDVersion", NsInfo, "1.0");
        writer.WriteStartElement("sf", "IDFactura", NsInfo);
        writer.WriteStartElement("sf", "IDEmisorFacturaAnulada", NsInfo);
        writer.WriteElementString("sf", "NIF", NsInfo, company.TaxId ?? string.Empty);
        writer.WriteEndElement();
        writer.WriteElementString("sf", "NumSerieFacturaAnulada", NsInfo, inv.Number);
        writer.WriteElementString("sf", "FechaExpedicionFacturaAnulada", NsInfo,
            inv.IssueDate.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture));
        writer.WriteEndElement();
        WriteEncadenamiento(writer, company.TaxId ?? string.Empty, previous);
        WriteSistemaInformatico(writer);
        var fechaHora = (inv.VerifactuAnulacionAt ?? inv.LockedAt ?? inv.IssueDate)
            .ToString("yyyy-MM-ddTHH:mm:sszzz");
        writer.WriteElementString("sf", "FechaHoraHusoGenRegistro", NsInfo, fechaHora);
        writer.WriteElementString("sf", "TipoHuella", NsInfo, "01");
        writer.WriteElementString("sf", "Huella", NsInfo, inv.VerifactuAnulacionHuella ?? string.Empty);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
        writer.Flush();
        return sb.ToString();
    }

    private async Task<string> GenerateAsync(
        Guid companyId, int year, int month, Guid? invoiceId, CancellationToken ct)
    {
        var company = await _app.Companies
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == companyId, ct)
            ?? throw new InvalidOperationException($"Company {companyId} not found.");

        var from = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var to   = from.AddMonths(1);

        var query = _billing.Invoices
            .Include(i => i.InvoiceLines)
            .Include(i => i.RectifiedInvoice)
            .Where(i => i.CompanyId == companyId
                     && i.IssueDate >= from
                     && i.IssueDate < to
                     && i.Status != "Draft"
                     && i.VerifactuHuella != null
                     && i.VerifactuSubmittedAt == null);

        if (invoiceId.HasValue)
            query = query.Where(i => i.Id == invoiceId.Value);

        var invoices = await query
            .OrderBy(i => i.SequenceNumber)
            .ToListAsync(ct);

        var sb = new StringBuilder();
        var settings = new XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8, OmitXmlDeclaration = false };
        using var writer = XmlWriter.Create(sb, settings);
        writer.WriteStartDocument();

        writer.WriteStartElement("sfLR", "RegFactuSistemaFacturacion", NsLR);
        writer.WriteAttributeString("xmlns", "sfLR", null, NsLR);
        writer.WriteAttributeString("xmlns", "sf", null, NsInfo);

        WriteCabecera(writer, company.Name, company.TaxId ?? string.Empty);

        foreach (var inv in invoices)
        {
            var previous = await GetPreviousRegistroAsync(
                inv.CompanyId, inv.Series, inv.FiscalYear, inv.LockedAt ?? inv.IssueDate, ct);
            WriteRegistroFactura(writer, inv, company.Name, company.TaxId ?? string.Empty, previous);
        }

        writer.WriteEndElement(); // RegFactuSistemaFacturacion
        writer.WriteEndDocument();
        writer.Flush();
        return sb.ToString();
    }

    private static void WriteCabecera(XmlWriter writer, string companyName, string companyNif)
    {
        writer.WriteStartElement("sfLR", "Cabecera", NsLR);
        writer.WriteStartElement("sf", "ObligadoEmision", NsInfo);
        writer.WriteElementString("sf", "NombreRazon", NsInfo, companyName);
        writer.WriteElementString("sf", "NIF", NsInfo, companyNif);
        writer.WriteEndElement(); // ObligadoEmision
        writer.WriteEndElement(); // Cabecera
    }

    private void WriteRegistroFactura(
        XmlWriter writer,
        Invoice inv,
        string companyName,
        string companyNif,
        PreviousRegistro? previous)
    {
        writer.WriteStartElement("sfLR", "RegistroFactura", NsLR);
        writer.WriteStartElement("sf", "RegistroAlta", NsInfo);

        writer.WriteElementString("sf", "IDVersion", NsInfo, "1.0");

        writer.WriteStartElement("sf", "IDFactura", NsInfo);
        writer.WriteStartElement("sf", "IDEmisorFactura", NsInfo);
        writer.WriteElementString("sf", "NIF", NsInfo, companyNif);
        writer.WriteEndElement();
        writer.WriteElementString("sf", "NumSerieFactura", NsInfo, inv.Number);
        writer.WriteElementString("sf", "FechaExpedicionFactura", NsInfo,
            inv.IssueDate.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture));
        writer.WriteEndElement(); // IDFactura

        writer.WriteElementString("sf", "NombreRazonEmisor", NsInfo, companyName);

        var tipoFactura = ResolveTipoFactura(inv);
        writer.WriteElementString("sf", "TipoFactura", NsInfo, tipoFactura);

        if (tipoFactura is "R1" or "R5")
            WriteFacturasRectificadas(writer, inv, companyNif);

        writer.WriteElementString("sf", "DescripcionOperacion", NsInfo,
            $"Factura {inv.Number}");

        WriteDesglose(writer, inv);

        writer.WriteElementString("sf", "CuotaTotal", NsInfo,
            inv.TaxAmount.ToString("F2", CultureInfo.InvariantCulture));
        writer.WriteElementString("sf", "ImporteTotal", NsInfo,
            inv.Total.ToString("F2", CultureInfo.InvariantCulture));

        WriteEncadenamiento(writer, companyNif, previous);

        WriteSistemaInformatico(writer);

        var fechaHora = (inv.LockedAt ?? inv.IssueDate).ToString("yyyy-MM-ddTHH:mm:sszzz");
        writer.WriteElementString("sf", "FechaHoraHusoGenRegistro", NsInfo, fechaHora);
        writer.WriteElementString("sf", "TipoHuella", NsInfo, "01");
        writer.WriteElementString("sf", "Huella", NsInfo, inv.VerifactuHuella ?? string.Empty);

        writer.WriteEndElement(); // RegistroAlta
        writer.WriteEndElement(); // RegistroFactura
    }

    private static string ResolveTipoFactura(Invoice inv) =>
        VerifactuTipoFactura.Resolve(inv.InvoiceType, inv.RectifiedInvoice?.InvoiceType);

    private void WriteFacturasRectificadas(XmlWriter writer, Invoice inv, string companyNif)
    {
        if (inv.RectifiedInvoice is null && inv.RectifiedInvoiceId.HasValue)
            throw new InvalidOperationException(
                $"Factura rectificativa {inv.Number}: falta factura original (RectifiedInvoice).");

        var orig = inv.RectifiedInvoice
            ?? throw new InvalidOperationException(
                $"Factura rectificativa {inv.Number}: factura original no cargada.");

        writer.WriteStartElement("sf", "FacturasRectificadas", NsInfo);
        writer.WriteStartElement("sf", "IDFacturaRectificada", NsInfo);
        writer.WriteStartElement("sf", "IDEmisorFactura", NsInfo);
        writer.WriteElementString("sf", "NIF", NsInfo, companyNif);
        writer.WriteEndElement();
        writer.WriteElementString("sf", "NumSerieFactura", NsInfo, orig.Number);
        writer.WriteElementString("sf", "FechaExpedicionFactura", NsInfo,
            orig.IssueDate.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture));
        writer.WriteEndElement(); // IDFacturaRectificada
        writer.WriteEndElement(); // FacturasRectificadas

        // S = por sustitución (abono total); I = por diferencias
        var tipoRect = inv.Total < 0 || inv.Subtotal < 0 ? "S" : "I";
        writer.WriteElementString("sf", "TipoRectificativa", NsInfo, tipoRect);
    }

    private static void WriteDesglose(XmlWriter writer, Invoice inv)
    {
        writer.WriteStartElement("sf", "DesgloseFactura", NsInfo);
        writer.WriteStartElement("sf", "DesgloseTipoOperacion", NsInfo);
        writer.WriteStartElement("sf", "Entrega", NsInfo);
        writer.WriteStartElement("sf", "Sujeta", NsInfo);
        writer.WriteStartElement("sf", "NoExenta", NsInfo);
        writer.WriteElementString("sf", "TipoNoExenta", NsInfo, "S1");
        writer.WriteStartElement("sf", "DesgloseIVA", NsInfo);
        writer.WriteStartElement("sf", "DetalleIVA", NsInfo);
        writer.WriteElementString("sf", "TipoImpositivo", NsInfo,
            inv.InvoiceLines.FirstOrDefault()?.TaxRate.ToString("F2", CultureInfo.InvariantCulture) ?? "21.00");
        writer.WriteElementString("sf", "BaseImponible", NsInfo,
            inv.Subtotal.ToString("F2", CultureInfo.InvariantCulture));
        writer.WriteElementString("sf", "CuotaRepercutida", NsInfo,
            inv.TaxAmount.ToString("F2", CultureInfo.InvariantCulture));
        writer.WriteEndElement(); // DetalleIVA
        writer.WriteEndElement(); // DesgloseIVA
        writer.WriteEndElement(); // NoExenta
        writer.WriteEndElement(); // Sujeta
        writer.WriteEndElement(); // Entrega
        writer.WriteEndElement(); // DesgloseTipoOperacion
        writer.WriteEndElement(); // DesgloseFactura
    }

    private static void WriteEncadenamiento(XmlWriter writer, string companyNif, PreviousRegistro? previous)
    {
        writer.WriteStartElement("sf", "Encadenamiento", NsInfo);
        if (previous is null)
        {
            writer.WriteElementString("sf", "PrimerRegistro", NsInfo, "S");
        }
        else
        {
            writer.WriteStartElement("sf", "RegistroAnterior", NsInfo);
            writer.WriteStartElement("sf", "IDEmisorFactura", NsInfo);
            writer.WriteElementString("sf", "NIF", NsInfo, companyNif);
            writer.WriteEndElement();
            writer.WriteElementString("sf", "NumSerieFactura", NsInfo, previous.NumSerieFactura);
            writer.WriteElementString("sf", "FechaExpedicionFactura", NsInfo, previous.FechaExpedicion);
            writer.WriteElementString("sf", "Huella", NsInfo, previous.Huella);
            writer.WriteEndElement(); // RegistroAnterior
        }
        writer.WriteEndElement(); // Encadenamiento
    }

    private void WriteSistemaInformatico(XmlWriter writer)
    {
        writer.WriteStartElement("sf", "SistemaInformatico", NsInfo);
        writer.WriteElementString("sf", "NombreRazon", NsInfo, _nombreSoftware);
        writer.WriteElementString("sf", "NIF", NsInfo, _nifSoftware);
        writer.WriteElementString("sf", "NombreSistemaInformatico", NsInfo, _nombreSoftware);
        writer.WriteElementString("sf", "IdSistemaInformatico", NsInfo, _idSistema);
        writer.WriteElementString("sf", "Version", NsInfo, _version);
        writer.WriteElementString("sf", "NumeroInstalacion", NsInfo, _numeroInstalacion);
        writer.WriteElementString("sf", "TipoUsoPosibleSoloVerifactu", NsInfo, _realtimeSubmission ? "S" : "N");
        writer.WriteElementString("sf", "TipoUsoPosibleMultiOT", NsInfo, "N");
        writer.WriteElementString("sf", "IndicadorMultiplesOT", NsInfo, "N");
        writer.WriteEndElement();
    }

    private async Task<PreviousRegistro?> GetPreviousRegistroAsync(
        Guid companyId,
        string series,
        int fiscalYear,
        DateTime beforeUtc,
        CancellationToken ct)
    {
        var prev = await VerifactuChainHelper.GetLastEntryBeforeAsync(
            _billing, companyId, series, fiscalYear, beforeUtc, ct);

        return prev is null
            ? null
            : new PreviousRegistro(prev.NumSerieFactura, prev.FechaExpedicion, prev.Huella);
    }

    private sealed record PreviousRegistro(string NumSerieFactura, DateTime IssueDate, string Huella)
    {
        public string FechaExpedicion => IssueDate.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
    }
}
