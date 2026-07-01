using Microsoft.AspNetCore.Http;

namespace Erp.Infrastructure.Fiscal;

/// <summary>
/// Marca respuestas HTTP de exportes fiscales como no oficiales hasta validación externa (AEAT/TGSS/asesoría).
/// </summary>
public static class FiscalExportHeaders
{
    public const string ExportDisclaimerHeader = "X-Fiscal-Export-Disclaimer";
    public const string OfficialFormatHeader = "X-Fiscal-Official-Format";

    /// <param name="shortReason">Texto breve (ideal &lt; 500 caracteres para cabeceras restrictivas).</param>
    public static void MarkAsNonOfficial(IHeaderDictionary headers, string shortReason)
    {
        headers[OfficialFormatHeader] = "false";
        var text = shortReason.Length > 500 ? shortReason[..500] : shortReason;
        headers[ExportDisclaimerHeader] = text;
    }
}
