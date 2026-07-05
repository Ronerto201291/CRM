using ClosedXML.Excel;
using Erp.Application.Common.Validation;
using System.Text;

namespace Erp.Modules.Crm.Application.Features.Onboarding;

public sealed record OnboardingClientRow(string Name, string TaxId, string? Email, string? Phone, string? Address);

/// <summary>Parser CSV y Excel (.xlsx) para import onboarding (#41).</summary>
public static class OnboardingImportParser
{
    public static IReadOnlyList<OnboardingClientRow> ParseCsv(string csvContent)
    {
        var rows = new List<OnboardingClientRow>();
        var lines = csvContent
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Skip(1);

        foreach (var line in lines)
        {
            var cols = line.Split(';', ',');
            if (cols.Length < 2) continue;
            rows.Add(MapColumns(cols));
        }

        return rows;
    }

    public static IReadOnlyList<OnboardingClientRow> ParseExcel(byte[] fileBytes)
    {
        using var stream = new MemoryStream(fileBytes);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.First();
        var rows = new List<OnboardingClientRow>();

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= lastRow; r++)
        {
            var name = sheet.Cell(r, 1).GetString().Trim();
            var taxId = sheet.Cell(r, 2).GetString().Trim();
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(taxId))
                continue;

            rows.Add(new OnboardingClientRow(
                name,
                taxId,
                NullIfEmpty(sheet.Cell(r, 3).GetString()),
                NullIfEmpty(sheet.Cell(r, 4).GetString()),
                NullIfEmpty(sheet.Cell(r, 5).GetString())));
        }

        return rows;
    }

    public static (bool Valid, string? Error) ValidateRow(OnboardingClientRow row)
    {
        if (string.IsNullOrWhiteSpace(row.Name))
            return (false, "nombre vacío");
        if (!SpanishTaxIdValidator.IsValid(row.TaxId))
            return (false, "CIF/NIF inválido");
        if (!string.IsNullOrWhiteSpace(row.Email) && !row.Email.Contains('@'))
            return (false, "email inválido");
        return (true, null);
    }

    private static OnboardingClientRow MapColumns(string[] cols) =>
        new(
            cols[0].Trim(),
            cols[1].Trim(),
            cols.Length > 2 ? NullIfEmpty(cols[2]) : null,
            cols.Length > 3 ? NullIfEmpty(cols[3]) : null,
            cols.Length > 4 ? NullIfEmpty(cols[4]) : null);

    private static string? NullIfEmpty(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
