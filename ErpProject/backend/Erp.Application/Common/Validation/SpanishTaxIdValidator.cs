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
        if (normalized.Length is not 9) return false;

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
        var controlDigit = (char)('0' + control);
        var controlLetter = CifLetters[control];

        return cif[0] switch
        {
            // Sociedades anónimas/limitadas, etc.: control numérico
            'A' or 'B' or 'E' or 'H' => cif[8] == controlDigit,
            // Comunidades de bienes, etc.: control alfabético
            'K' or 'L' or 'M' or 'N' or 'P' or 'Q' or 'R' or 'S' or 'W' => cif[8] == controlLetter,
            // Resto: admite dígito o letra
            _ => cif[8] == controlDigit || cif[8] == controlLetter
        };
    }

    [GeneratedRegex(@"^[0-9]{8}[A-Z]$")]
    private static partial Regex NifRegex();

    [GeneratedRegex(@"^[XYZ][0-9]{7}[A-Z]$")]
    private static partial Regex NieRegex();

    [GeneratedRegex(@"^[ABCDEFGHJNPQRSUVW][0-9]{7}[0-9A-J]$")]
    private static partial Regex CifRegex();
}
