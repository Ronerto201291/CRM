using System.Text.RegularExpressions;

namespace Erp.Application.Common.Validation;

/// <summary>Validación de NIF/CIF/NIE español (dígito/letra de control).</summary>
public static partial class SpanishTaxIdValidator
{
    private const string CifLetters = "JABCDEFGHI";

    public static bool IsValid(string? taxId)
    {
        if (string.IsNullOrWhiteSpace(taxId)) return false;
        var normalized = taxId.Trim().ToUpperInvariant().Replace(" ", "").Replace("-", "");
        if (normalized.Length is < 9 or > 9) return false;

        if (NieRegex().IsMatch(normalized))
            return ValidateNie(normalized);

        if (NifRegex().IsMatch(normalized))
            return ValidateNif(normalized);

        if (CifRegex().IsMatch(normalized))
            return ValidateCif(normalized);

        return false;
    }

    private static bool ValidateNif(string nif)
    {
        var number = int.Parse(nif[..8]);
        var expected = "TRWAGMYFPDXBNJZSQVHLCKE"[number % 23];
        return nif[8] == expected;
    }

    private static bool ValidateNie(string nie)
    {
        var prefix = nie[0] switch
        {
            'X' => '0',
            'Y' => '1',
            'Z' => '2',
            _ => '\0'
        };
        if (prefix == '\0') return false;
        return ValidateNif($"{prefix}{nie[1..]}");
    }

    private static bool ValidateCif(string cif)
    {
        var digits = cif[1..8];
        var sumEven = 0;
        var sumOdd = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var n = digits[i] - '0';
            if (i % 2 == 0)
            {
                var doubled = n * 2;
                sumOdd += doubled / 10 + doubled % 10;
            }
            else
            {
                sumEven += n;
            }
        }

        var control = (10 - (sumEven + sumOdd) % 10) % 10;
        var digitChar = (char)('0' + control);
        var letterChar = CifLetters[control];

        // A/B/E/H (sociedades anónimas, limitadas, comunidades de bienes,
        // sociedades civiles) llevan siempre dígito de control; P/Q/S
        // (organismos públicos, corporaciones, cooperativas) llevan siempre
        // letra; el resto (C/D/F/G/J/N/R/U/V/W) acepta cualquiera de los dos.
        return cif[0] switch
        {
            'A' or 'B' or 'E' or 'H' => cif[8] == digitChar,
            'P' or 'Q' or 'S' => cif[8] == letterChar,
            _ => cif[8] == digitChar || cif[8] == letterChar
        };
    }

    [GeneratedRegex(@"^[0-9]{8}[A-Z]$")]
    private static partial Regex NifRegex();

    [GeneratedRegex(@"^[XYZ][0-9]{7}[A-Z]$")]
    private static partial Regex NieRegex();

    [GeneratedRegex(@"^[ABCDEFGHJNPQRSUVW][0-9]{7}[0-9A-J]$")]
    private static partial Regex CifRegex();
}
