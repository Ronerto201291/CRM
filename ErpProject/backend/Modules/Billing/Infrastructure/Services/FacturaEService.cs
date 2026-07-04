using Erp.Application.Common.Fiscal;
using Erp.Application.Common.Interfaces;
using Erp.Application.Common.Validation;
using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Services.Sii;
using Erp.Modules.Billing.Application.Interfaces;
using Erp.Modules.Billing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace Erp.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Generates FacturaE 3.2.2 XML documents.
/// Required by Ley 18/2022 Crea y Crece for B2B electronic invoicing between Spanish companies.
/// Spec: https://www.facturae.gob.es/formato/Versiones/Esquema_castellano_v3.2.2_0.pdf
/// </summary>
public sealed class FacturaEService : IFacturaEService
{
    private static readonly CultureInfo Inv   = CultureInfo.InvariantCulture;
    // Referencia la constante de FacturaEXmlStructureValidator en vez de un
    // literal propio, para que generador y validador no puedan volver a
    // divergir (antes ambos hardcodeaban el mismo namespace incorrecto por
    // separado, lo que hacía que la validación fuera circular).
    private static readonly XNamespace  FE    = FacturaEXmlStructureValidator.FacturaENamespace;
    private static readonly XNamespace  DS    = "http://www.w3.org/2000/09/xmldsig#";

    private readonly IBillingDbContext     _billing;
    private readonly IApplicationDbContext _app;
    private readonly ISiiSigningService    _signer;

    public FacturaEService(
        IBillingDbContext billing,
        IApplicationDbContext app,
        ISiiSigningService signer)
    {
        _billing = billing;
        _app     = app;
        _signer  = signer;
    }

    public Task<(byte[] XmlBytes, string FileName)> GenerateAsync(
        Guid invoiceId, Guid tenantId, CancellationToken ct = default) =>
        GenerateCoreAsync(invoiceId, tenantId, sign: false, ct);

    public Task<(byte[] XmlBytes, string FileName)> GenerateSignedAsync(
        Guid invoiceId, Guid tenantId, CancellationToken ct = default) =>
        GenerateCoreAsync(invoiceId, tenantId, sign: true, ct);

    private async Task<(byte[] XmlBytes, string FileName)> GenerateCoreAsync(
        Guid invoiceId, Guid tenantId, bool sign, CancellationToken ct)
    {
        var invoice = await _billing.Invoices
            .Include(i => i.InvoiceLines)
            .Where(i => i.Id == invoiceId && i.CompanyId == tenantId)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException($"Factura {invoiceId} no encontrada.");

        if (!invoice.IsLocked)
            throw new InvalidOperationException(
                "La factura debe estar bloqueada (IsLocked=true) para generar el FacturaE. " +
                "Ley 11/2021 Antifraude exige inmutabilidad.");

        var company = await _app.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == tenantId, ct);

        ValidateTaxId(company?.TaxId, "emisor");
        ValidateTaxId(invoice.ClientNif, "cliente");

        var xml = BuildFacturaE(invoice, company);
        var xmlString = xml.Declaration + "\n" + xml.ToString();

        if (sign)
        {
            if (!_signer.IsConfigured)
                throw new InvalidOperationException(
                    "Certificado de firma no configurado. Configure Sii:CertPath y Sii:CertPass.");
            xmlString = _signer.Sign(xmlString);
        }

        var bytes = Encoding.UTF8.GetBytes(xmlString);
        var ext = sign ? "xsig" : "xml";
        var fileName = $"FacturaE_{invoice.Number.Replace("/", "-")}_{invoice.IssueDate:yyyyMMdd}.{ext}";

        return (bytes, fileName);
    }

    // ── XML builder ────────────────────────────────────────────────────────────

    private static XDocument BuildFacturaE(
        Invoice invoice,
        Company? company)
    {
        // ── aggregated tax totals ──────────────────────────────────────────────
        var taxGroups = invoice.InvoiceLines
            .GroupBy(l => l.TaxRate)
            .Select(g => (
                Rate:  g.Key,
                Base:  g.Sum(l => l.LineTotal),
                Cuota: g.Sum(l => l.TaxAmount)))
            .ToList();

        string Fmt(decimal d) => d.ToString("N2", Inv);
        string FmtQty(decimal d) => d.ToString("N6", Inv);

        // ── document type (FacturaE 3.2.2) ────────────────────────────────────
        var (docType, docClass) = invoice.InvoiceType switch
        {
            "Rectificativa" => ("AF", "OR"), // Abono / Corrección
            "Simplificada"  => ("FA", "OO"), // Factura simplificada
            _               => ("FC", "OO"), // Factura completa
        };

        // ── parties ───────────────────────────────────────────────────────────
        var sellerNif     = invoice.CompanyNif  ?? company?.TaxId ?? string.Empty;
        var sellerName    = invoice.CompanyName ?? company?.Name  ?? string.Empty;
        var sellerAddress = invoice.CompanyAddress ?? company?.Address ?? string.Empty;
        var buyerNif      = invoice.ClientNif  ?? string.Empty;
        var buyerName     = invoice.ClientName ?? string.Empty;
        var buyerAddress  = invoice.ClientAddress ?? string.Empty;

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(FE + "Facturae",
                new XAttribute(XNamespace.Xmlns + "fe", FE.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "ds", DS.NamespaceName),

                // ── FileHeader ────────────────────────────────────────────────
                new XElement(FE + "FileHeader",
                    new XElement(FE + "SchemaVersion", "3.2.2"),
                    new XElement(FE + "Modality", "I"),
                    new XElement(FE + "InvoiceIssuerType", "EM"),
                    new XElement(FE + "Batch",
                        new XElement(FE + "BatchIdentifier",
                            $"{sellerNif}{invoice.Number}"),
                        new XElement(FE + "InvoicesCount", "1"),
                        new XElement(FE + "TotalInvoicesAmount",
                            new XElement(FE + "TotalAmount", Fmt(invoice.Total))),
                        new XElement(FE + "TotalOutstandingAmount",
                            new XElement(FE + "TotalAmount", Fmt(invoice.Total))),
                        new XElement(FE + "TotalExecutableAmount",
                            new XElement(FE + "TotalAmount", Fmt(invoice.Total))),
                        new XElement(FE + "InvoiceCurrencyCode", "EUR")),
                    RepresentacionGraficaExtension(FE, invoice, Fmt)),

                // ── Parties ───────────────────────────────────────────────────
                new XElement(FE + "Parties",
                    PartyElement(FE, "SellerParty", sellerNif, sellerName,
                        sellerAddress, company?.Country ?? "ES"),
                    PartyElement(FE, "BuyerParty",  buyerNif,  buyerName,
                        buyerAddress)),

                // ── Invoices ──────────────────────────────────────────────────
                new XElement(FE + "Invoices",
                    new XElement(FE + "Invoice",

                        // InvoiceHeader
                        new XElement(FE + "InvoiceHeader",
                            new XElement(FE + "InvoiceNumber",     invoice.Number),
                            new XElement(FE + "InvoiceSeriesCode", invoice.Series),
                            new XElement(FE + "InvoiceDocumentType", docType),
                            new XElement(FE + "InvoiceClass", docClass)),

                        // InvoiceIssueData
                        new XElement(FE + "InvoiceIssueData",
                            new XElement(FE + "IssueDate",
                                invoice.IssueDate.ToString("yyyy-MM-dd")),
                            invoice.OperationDate.HasValue
                                ? new XElement(FE + "OperationDate",
                                    invoice.OperationDate.Value.ToString("yyyy-MM-dd"))
                                : null,
                            new XElement(FE + "InvoiceCurrencyCode", "EUR"),
                            new XElement(FE + "TaxCurrencyCode", "EUR"),
                            new XElement(FE + "LanguageName", "es")),

                        // TaxesOutputs (IVA)
                        taxGroups.Count > 0
                            ? new XElement(FE + "TaxesOutputs",
                                taxGroups.Select(t =>
                                    new XElement(FE + "Tax",
                                        new XElement(FE + "TaxTypeCode", "01"),
                                        new XElement(FE + "TaxRate",     Fmt(t.Rate)),
                                        new XElement(FE + "TaxableBase",
                                            new XElement(FE + "TotalAmount", Fmt(t.Base))),
                                        new XElement(FE + "TaxAmount",
                                            new XElement(FE + "TotalAmount", Fmt(t.Cuota))))))
                            : null,

                        // TaxesWithheld (IRPF)
                        invoice.IrpfAmount > 0
                            ? new XElement(FE + "TaxesWithheld",
                                new XElement(FE + "Tax",
                                    new XElement(FE + "TaxTypeCode", "04"),
                                    new XElement(FE + "TaxRate",
                                        Fmt(invoice.IrpfRate)),
                                    new XElement(FE + "TaxableBase",
                                        new XElement(FE + "TotalAmount",
                                            Fmt(invoice.Subtotal))),
                                    new XElement(FE + "TaxAmount",
                                        new XElement(FE + "TotalAmount",
                                            Fmt(invoice.IrpfAmount)))))
                            : null,

                        // InvoiceTotals
                        new XElement(FE + "InvoiceTotals",
                            new XElement(FE + "TotalGrossAmount",
                                Fmt(invoice.Subtotal)),
                            new XElement(FE + "TotalGrossAmountBeforeTaxes",
                                Fmt(invoice.Subtotal)),
                            new XElement(FE + "TotalTaxOutputs",
                                Fmt(invoice.TaxAmount)),
                            new XElement(FE + "TotalTaxesWithheld",
                                Fmt(invoice.IrpfAmount)),
                            new XElement(FE + "InvoiceTotal",
                                Fmt(invoice.Total)),
                            new XElement(FE + "TotalOutstandingAmount",
                                Fmt(invoice.Total)),
                            new XElement(FE + "TotalExecutableAmount",
                                Fmt(invoice.Total))),

                        // Items
                        new XElement(FE + "Items",
                            invoice.InvoiceLines.Select((line, idx) =>
                                new XElement(FE + "InvoiceLine",
                                    new XElement(FE + "ItemDescription",
                                        line.Description),
                                    new XElement(FE + "Quantity",
                                        FmtQty(line.Quantity)),
                                    new XElement(FE + "UnitOfMeasure", "01"),
                                    new XElement(FE + "UnitPriceWithoutTax",
                                        Fmt(line.UnitPrice)),
                                    new XElement(FE + "TotalCost",
                                        Fmt(line.LineTotal)),
                                    new XElement(FE + "GrossAmount",
                                        Fmt(line.LineTotal)),
                                    line.TaxRate > 0
                                        ? new XElement(FE + "TaxesOutputs",
                                            new XElement(FE + "Tax",
                                                new XElement(FE + "TaxTypeCode", "01"),
                                                new XElement(FE + "TaxRate",
                                                    Fmt(line.TaxRate)),
                                                new XElement(FE + "TaxableBase",
                                                    new XElement(FE + "TotalAmount",
                                                        Fmt(line.LineTotal))),
                                                new XElement(FE + "TaxAmount",
                                                    new XElement(FE + "TotalAmount",
                                                        Fmt(line.TaxAmount)))))
                                        : null))),

                        // LegalLiterals
                        new XElement(FE + "LegalLiterals",
                            new XElement(FE + "LegalReference",
                                "Factura emitida conforme a Ley 37/1992 del IVA y RD 1619/2012"),
                            invoice.VerifactuHuella is not null
                                ? new XElement(FE + "LegalReference",
                                    $"Verifactu RD 1007/2023: {invoice.VerifactuHuella[..16]}…")
                                : null)))));

        return doc;
    }

    /// <summary>
    /// Representación gráfica estructurada (referencia RD 1007/2023 / Verifactu).
    /// Incrustada en Extensions del FileHeader según perfil FacturaE (ExtensionContent).
    /// </summary>
    private static XElement RepresentacionGraficaExtension(
        XNamespace fe, Invoice invoice, Func<decimal, string> fmtDec)
    {
        var sb = new StringBuilder();
        sb.Append("RepresentacionGrafica|1.0|");
        sb.Append(invoice.Number).Append('|');
        sb.Append(invoice.IssueDate.ToString("yyyy-MM-dd", Inv)).Append('|');
        sb.Append(invoice.CompanyName ?? "").Append('|');
        sb.Append(invoice.ClientName ?? "").Append('|');
        sb.Append(fmtDec(invoice.Subtotal)).Append('|');
        sb.Append(fmtDec(invoice.TaxAmount)).Append('|');
        sb.Append(fmtDec(invoice.Total)).Append("||");
        foreach (var l in invoice.InvoiceLines)
        {
            var desc = (l.Description ?? "").Replace('|', '/');
            sb.Append(desc).Append(';')
                .Append(fmtDec(l.Quantity)).Append(';')
                .Append(fmtDec(l.UnitPrice)).Append(';')
                .Append(fmtDec(l.LineTotal)).Append(';')
                .Append(fmtDec(l.TaxRate)).Append("%|");
        }

        return new XElement(fe + "Extensions",
            new XElement(fe + "Extension",
                new XElement(fe + "ExtensionCode", "VERIFACTU-RG"),
                new XElement(fe + "ExtensionName", "RepresentacionGrafica"),
                new XElement(fe + "ExtensionContent",
                    new XCData(sb.ToString()))));
    }

    // ── helper: build a SellerParty / BuyerParty element ─────────────────────

    private static XElement PartyElement(
        XNamespace ns, string tag, string nif, string name,
        string address, string country = "ES")
    {
        var personType = IsCompanyNif(nif) ? "J" : "F";
        var residence  = GetResidenceCode(country);
        var countryCode = CountryCodeToIso3(country);

        // Choose AddressInSpain vs OverseasAddress
        var town = ExtractTown(address);
        var postCode = ExtractPostCode(address);

        XElement addressEl = string.Equals(country, "ES", StringComparison.OrdinalIgnoreCase)
            ? new XElement(ns + "AddressInSpain",
                new XElement(ns + "Address",  address.Length > 80 ? address[..80] : address),
                new XElement(ns + "PostCode",  postCode),
                new XElement(ns + "Town",      town),
                new XElement(ns + "Province",  town),
                new XElement(ns + "CountryCode", "ESP"))
            : new XElement(ns + "OverseasAddress",
                new XElement(ns + "Address",  address.Length > 80
                    ? address[..80] : address),
                new XElement(ns + "PostCodeAndTown", "N/D"),
                new XElement(ns + "Province",  "N/D"),
                new XElement(ns + "CountryCode", countryCode));

        // Individual vs LegalEntity based on NIF type
        var entityEl = personType == "J"
            ? new XElement(ns + "LegalEntity",
                new XElement(ns + "CorporateName",
                    name.Length > 80 ? name[..80] : name),
                addressEl)
            : new XElement(ns + "Individual",
                new XElement(ns + "Name",
                    name.Length > 40 ? name[..40] : name),
                new XElement(ns + "FirstSurname", "N/D"),
                addressEl);

        return new XElement(ns + tag,
            new XElement(ns + "TaxIdentification",
                new XElement(ns + "PersonTypeCode",       personType),
                new XElement(ns + "ResidenceTypeCode",    residence),
                new XElement(ns + "TaxIdentificationNumber", nif)),
            entityEl);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static string ExtractPostCode(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) return "00000";
        var match = System.Text.RegularExpressions.Regex.Match(address, @"\b(\d{5})\b");
        return match.Success ? match.Groups[1].Value : "00000";
    }

    private static void ValidateTaxId(string? taxId, string role)
    {
        if (string.IsNullOrWhiteSpace(taxId)) return;
        if (!SpanishTaxIdValidator.IsValid(taxId))
            throw new InvalidOperationException($"NIF/CIF/NIE del {role} no válido: {taxId}");
    }

    private static string ExtractTown(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) return "Desconocido";
        var parts = address.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1)
        {
            var last = parts[^1];
            return last.Length > 50 ? last[..50] : last;
        }
        return address.Length > 50 ? address[..50] : address;
    }

    /// <summary>
    /// Heuristic: Spanish company NIFs start with a letter (B, A, C, …) or are 9 chars.
    /// Foreign EU VAT numbers also start with country code letters.
    /// Física = DNI (8 digits + letter) or NIE (X/Y/Z + 7 digits + letter).
    /// </summary>
    private static bool IsCompanyNif(string nif)
    {
        if (string.IsNullOrWhiteSpace(nif)) return true;
        var first = char.ToUpperInvariant(nif[0]);
        // DNI: starts with digit; NIE: X, Y, Z
        return first != 'X' && first != 'Y' && first != 'Z' && !char.IsDigit(first);
    }

    private static string GetResidenceCode(string country)
    {
        var c = country.ToUpperInvariant();
        if (c == "ES") return "R";  // Residente España
        var euCountries = new HashSet<string>
        {
            "AT","BE","BG","CY","CZ","DE","DK","EE","FI","FR","GR","HR",
            "HU","IE","IT","LT","LU","LV","MT","NL","PL","PT","RO","SE","SI","SK"
        };
        return euCountries.Contains(c) ? "U" : "E";
    }

    private static string CountryCodeToIso3(string iso2) => iso2.ToUpperInvariant() switch
    {
        "ES" => "ESP", "FR" => "FRA", "DE" => "DEU", "IT" => "ITA",
        "PT" => "PRT", "GB" => "GBR", "US" => "USA", "MX" => "MEX",
        "NL" => "NLD", "BE" => "BEL", "PL" => "POL", "SE" => "SWE",
        var x when x.Length == 3 => x,   // already 3-letter
        var x => x.PadRight(3, 'X')      // fallback (unknown)
    };
}
