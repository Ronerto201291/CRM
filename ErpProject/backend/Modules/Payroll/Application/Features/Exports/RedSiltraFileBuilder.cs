using System.Globalization;
using System.Text;

namespace Erp.Modules.Payroll.Application.Features.Exports;

/// <summary>
/// Generador pragmático de fichero RED/SILTRA de cotización (registros longitud fija 250, ISO-8859-1).
/// Estructura orientativa inspirada en ficheros planos históricos; SILTRA oficial usa XML homologado.
/// No sustituye homologación TGSS — ver disclaimer en ExportRedHandler y docs/payroll-red-siltra.md.
/// </summary>
public static class RedSiltraFileBuilder
{
    public const int RecordLength = 250;
    public const string EncodingName = "ISO-8859-1";

    /// <summary>Layout registro 01: cabecera empresa (posiciones 1-based tras tipo).</summary>
    /// <remarks>02-12 CCC | 13-18 periodo AAAAMM | 19-20 régimen | 21-23 tipo liq. | 24-32 NIF | 33-72 razón social | 73-78 nº trab. | 79-250 reserva.</remarks>
    public static byte[] Build(
        string employerCcc,
        string employerTaxId,
        string employerName,
        int year,
        int month,
        IReadOnlyList<PayrollExportLine> lines)
    {
        var sb = new StringBuilder();
        var period = $"{year}{month:D2}";
        var ccc = PadDigits(employerCcc, 11);
        var regime = "01";
        var liquidationType = "L00";

        sb.AppendLine(BuildRecord("01",
            ccc, 11,
            period, 6,
            regime, 2,
            liquidationType, 3,
            NormalizeTaxId(employerTaxId), 9,
            Truncate(employerName, 40), 40,
            lines.Count.ToString(CultureInfo.InvariantCulture), 6));

        foreach (var line in lines)
        {
            // Registro 02: NAF | NIF | nombre | base CC | base AT/EP | cuota obrera | cuota empresa | bruto | días
            sb.AppendLine(BuildRecord("02",
                NormalizeNaf(line.SocialSecurityNumber), 12,
                NormalizeTaxId(line.TaxId), 9,
                Truncate(line.FullName, 40), 40,
                FormatAmount(line.CommonContingenciesBase), 11,
                FormatAmount(line.CommonContingenciesBase), 11,
                FormatAmount(line.EmployeeSocialSecurity), 11,
                FormatAmount(line.EmployerSocialSecurity), 11,
                FormatAmount(line.GrossSalary), 11,
                "30", 2));
        }

        var totalBase = lines.Sum(l => l.CommonContingenciesBase);
        var totalWorker = lines.Sum(l => l.EmployeeSocialSecurity);
        var totalEmployer = lines.Sum(l => l.EmployerSocialSecurity);
        var recordCount = lines.Count + 2;

        sb.AppendLine(BuildRecord("99",
            ccc, 11,
            period, 6,
            lines.Count.ToString(CultureInfo.InvariantCulture), 6,
            FormatAmount(totalBase), 13,
            FormatAmount(totalWorker), 13,
            FormatAmount(totalEmployer), 13,
            recordCount.ToString(CultureInfo.InvariantCulture), 6));

        return Encoding.GetEncoding(EncodingName).GetBytes(sb.ToString());
    }

    internal static string BuildRecord(string type, params object[] valueWidthPairs)
    {
        var body = new StringBuilder();
        for (var i = 0; i < valueWidthPairs.Length; i += 2)
        {
            var value = valueWidthPairs[i]?.ToString() ?? "";
            var width = (int)valueWidthPairs[i + 1];
            body.Append(PadField(value, width));
        }

        return PadField(type + body, RecordLength);
    }

    internal static string PadField(string value, int width)
    {
        if (value.Length > width)
            return value[..width];
        return value.PadRight(width, ' ');
    }

    internal static string FormatAmount(decimal amount) =>
        ((long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero))
        .ToString(CultureInfo.InvariantCulture)
        .PadLeft(11, '0');

    internal static string NormalizeTaxId(string? taxId)
    {
        var t = (taxId ?? "").Replace(" ", "").ToUpperInvariant();
        return t.Length > 9 ? t[..9] : t.PadRight(9);
    }

    internal static string NormalizeNaf(string? naf)
    {
        var n = new string((naf ?? "").Where(char.IsDigit).ToArray());
        return n.Length > 12 ? n[..12] : n.PadRight(12, '0');
    }

    private static string PadDigits(string value, int width)
    {
        var n = new string((value ?? "").Where(char.IsDigit).ToArray());
        return n.Length > width ? n[..width] : n.PadLeft(width, '0');
    }

    private static string Truncate(string? text, int max) =>
        string.IsNullOrEmpty(text) ? "" : text.Length <= max ? text : text[..max];
}
