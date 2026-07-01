using Erp.Application.Common.Interfaces;
using Microsoft.Extensions.Options;
using QRCoder;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;

namespace Erp.Infrastructure.Services;

/// <summary>
/// Verifactu implementation per RD 1007/2023 (Reglamento de Control Técnico de Software).
/// Covers:
///   - Hash chain (Huella) per invoice (SHA-256 of canonical field concatenation)
///   - QR code URL for invoice self-validation at AEAT
///   - QR code as SVG for embedding in PDF invoice
///
/// Software identification fields (required by regulation):
///   Config key Verifactu:NifSoftware — NIF of the software maker (Antigravity)
///   Config key Verifactu:NombreSoftware — software name
///   Config key Verifactu:IdSistema — system identifier (e.g. "ANTIGRAVITY-ERP-1")
/// </summary>
public class VerifactuService : IVerifactuService
{
    private readonly string _nifSoftware;
    private readonly string _idSistema;

    // Verifactu QR validation URL (AEAT)
    private const string QrBaseUrl =
        "https://www2.agenciatributaria.gob.es/wlpl/TIKE-CONT/ValidarQR";

    public VerifactuService(IOptions<VerifactuOptions> options)
    {
        var opts = options.Value;
        opts.AssertValid();
        _nifSoftware = opts.NifSoftware.Trim();
        _idSistema   = opts.IdSistema.Trim();
    }

    /// <summary>
    /// Computes the Verifactu Huella (hash) for an invoice.
    /// Field order and separator defined in RD 1007/2023 Annex II.
    ///
    /// Huella = SHA256_HEX(
    ///   NIF_Emisor &amp; NumSerie &amp; Fecha &amp; TipoFactura &amp;
    ///   CuotaTotal &amp; ImporteTotal &amp; HuellaAnterior &amp;
    ///   NIF_Software &amp; IdSistema &amp; NumRegistro &amp; FechaHora
    /// )
    /// </summary>
    public string ComputeHuella(VerifactuInvoiceData data)
    {
        // All decimal amounts formatted with exactly 2 decimal places and no thousands separator
        var cuotaTotal    = data.CuotaTotal.ToString("F2", CultureInfo.InvariantCulture);
        var importeTotal  = data.ImporteTotal.ToString("F2", CultureInfo.InvariantCulture);
        var fechaHora     = data.FechaHoraHuella.ToString("yyyy-MM-ddTHH:mm:sszzz");

        var campos = string.Join("&", new[]
        {
            data.NifEmisor,
            data.NumSerieFactura,
            data.FechaExpedicion.ToString("dd-MM-yyyy"),
            data.TipoFactura,
            cuotaTotal,
            importeTotal,
            data.HuellaAnterior ?? string.Empty,
            _nifSoftware,
            _idSistema,
            data.NumeroRegistro.ToString(),
            fechaHora
        });

        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(campos));
        return BitConverter.ToString(bytes).Replace("-", "").ToUpperInvariant();
    }

    /// <summary>
    /// Generates the AEAT Verifactu QR validation URL for an invoice.
    /// This URL is printed on the invoice (as QR code + plain text).
    /// </summary>
    public string GetQrUrl(VerifactuInvoiceData data, string huella)
    {
        var nif    = Uri.EscapeDataString(data.NifEmisor);
        var num    = Uri.EscapeDataString(data.NumSerieFactura);
        var fecha  = Uri.EscapeDataString(data.FechaExpedicion.ToString("dd-MM-yyyy"));
        var importe = Uri.EscapeDataString(
            data.ImporteTotal.ToString("F2", CultureInfo.InvariantCulture));
        var hash   = Uri.EscapeDataString(huella);

        return $"{QrBaseUrl}?nif={nif}&numserie={num}&fecha={fecha}&importe={importe}&huella={hash}";
    }

    /// <summary>
    /// Generates a QR code as SVG string for the given Verifactu URL.
    /// Safe for embedding in PDF/HTML without System.Drawing dependency.
    /// </summary>
    public string GenerateQrSvg(string qrUrl)
    {
        using var qrGenerator = new QRCodeGenerator();
        var qrData    = qrGenerator.CreateQrCode(qrUrl, QRCodeGenerator.ECCLevel.M);
        var svgCode   = new SvgQRCode(qrData);
        return svgCode.GetGraphic(4, darkColorHex: "#000000", lightColorHex: "#FFFFFF",
            drawQuietZones: true);
    }

    /// <summary>
    /// Full pipeline: compute huella → build QR URL → generate SVG.
    /// Returns a value object with all Verifactu artefacts for this invoice.
    /// </summary>
    public VerifactuResult Process(VerifactuInvoiceData data)
    {
        var huella = ComputeHuella(data);
        var url    = GetQrUrl(data, huella);
        var svg    = GenerateQrSvg(url);
        return new VerifactuResult(huella, url, svg);
    }

    /// <inheritdoc />
    public (string Huella, string QrUrl) Compute(
        string nifEmisor, string numSerieFactura, DateOnly fechaExpedicion,
        string tipoFactura, decimal cuotaTotal, decimal importeTotal,
        string? huellaAnterior, int numeroRegistro, DateTimeOffset fechaHoraHuella)
    {
        var data = new VerifactuInvoiceData(
            nifEmisor, numSerieFactura, fechaExpedicion, tipoFactura,
            cuotaTotal, importeTotal, huellaAnterior, numeroRegistro, fechaHoraHuella);
        var huella = ComputeHuella(data);
        var url    = GetQrUrl(data, huella);
        return (huella, url);
    }
}

/// <summary>Input data required to compute a Verifactu Huella.</summary>
public record VerifactuInvoiceData(
    string NifEmisor,
    string NumSerieFactura,
    DateOnly FechaExpedicion,
    string TipoFactura,          // F1, F2, R1, etc.
    decimal CuotaTotal,          // total VAT amount
    decimal ImporteTotal,        // invoice total (inc. VAT)
    string? HuellaAnterior,      // hash of previous invoice (null/empty for first)
    int NumeroRegistro,          // sequential counter (per NIF per year)
    DateTimeOffset FechaHoraHuella  // timestamp when hash was computed
);

/// <summary>Output artefacts produced by VerifactuService.Process().</summary>
public record VerifactuResult(
    string Huella,   // SHA-256 hex hash
    string QrUrl,    // AEAT validation URL
    string QrSvg     // SVG QR code string
);
