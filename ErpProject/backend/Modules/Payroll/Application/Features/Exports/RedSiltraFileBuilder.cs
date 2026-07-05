using System.Globalization;
using System.Text;

namespace Erp.Modules.Payroll.Application.Features.Exports;

/// <summary>
/// Generador pragmático de fichero RED/SILTRA de cotización (registros longitud fija 250).
/// No sustituye homologación TGSS real — ver disclaimer en ExportRedHandler.
/// </summary>
internal static class RedSiltraFileBuilder
{
    internal const int RecordLength = 250;

    internal static byte[] Build(
        string employerTaxId,
        string employerName,
        int year,
        int month,
        IReadOnlyList<PayrollExportLine> lines)
    {
        var sb = new StringBuilder();
        var period = $"{year}{month:D2}";
        var ccc = DeriveCcc(employerTaxId);

        sb.AppendLine(BuildRecord("01",
            ccc, 11,
            period, 6,
            NormalizeTaxId(employerTaxId), 9,
            Truncate(employerName, 40), 40,
            "LIQ", 3,
            lines.Count.ToString(CultureInfo.InvariantCulture), 6));

        foreach (var line in lines)
        {
            sb.AppendLine(BuildRecord("02",
                NormalizeNaf(line.SocialSecurityNumber), 12,
                NormalizeTaxId(line.TaxId), 9,
                Truncate(line.FullName, 40), 40,
                FormatAmount(line.CommonContingenciesBase), 11,
                FormatAmount(line.EmployeeSocialSecurity), 11,
                FormatAmount(line.EmployerSocialSecurity), 11,
                FormatAmount(line.GrossSalary), 11));
        }

        var totalBase = lines.Sum(l => l.CommonContingenciesBase);
        var totalWorker = lines.Sum(l => l.EmployeeSocialSecurity);
        var totalEmployer = lines.Sum(l => l.EmployerSocialSecurity);

        sb.AppendLine(BuildRecord("99",
            ccc, 11,
            period, 6,
            lines.Count.ToString(CultureInfo.InvariantCulture), 6,
            FormatAmount(totalBase), 13,
            FormatAmount(totalWorker), 13,
            FormatAmount(totalEmployer), 13));

        return Encoding.GetEncoding("ISO-8859-1").GetBytes(sb.ToString());
    }

    private static string BuildRecord(string type, params object[] valueWidthPairs)
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

    private static string PadField(string value, int width)
    {
        if (value.Length > width)
            return value[..width];
        return value.PadRight(width, ' ');
    }

    private static string FormatAmount(decimal amount) =>
        ((long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);

    private static string NormalizeTaxId(string? taxId)
    {
        var t = (taxId ?? "").Replace(" ", "").ToUpperInvariant();
        return t.Length > 9 ? t[..9] : t.PadRight(9);
    }

    private static string NormalizeNaf(string? naf)
    {
        var n = (naf ?? "").Replace(" ", "").Replace("/", "");
        return n.Length > 12 ? n[..12] : n.PadRight(12);
    }

    private static string DeriveCcc(string taxId)
    {
        var digits = new string((taxId ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length >= 11)
            return digits[..11];
        return digits.PadRight(11, '0');
    }

    private static string Truncate(string? text, int max) =>
        string.IsNullOrEmpty(text) ? "" : text.Length <= max ? text : text[..max];
}
