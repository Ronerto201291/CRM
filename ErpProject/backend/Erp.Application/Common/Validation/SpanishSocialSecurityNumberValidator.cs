using System.Globalization;

namespace Erp.Application.Common.Validation;

/// <summary>
/// Validación y dígitos de control de NAF/NUSS (12 dígitos) y CCC (11 dígitos) españoles (módulo 97).
/// No sustituye homologación TGSS/SILTRA.
/// </summary>
public static class SpanishSocialSecurityNumberValidator
{
    public static bool IsValidNaf(string? value) =>
        TryParseAndValidate(value, sequentialLength: 8, out _);

    public static bool IsValidCcc(string? value) =>
        TryParseAndValidate(value, sequentialLength: 7, out _);

    /// <summary>Normaliza a solo dígitos (elimina espacios, barras y guiones).</summary>
    public static string NormalizeDigits(string? value) =>
        new string((value ?? "").Where(char.IsDigit).ToArray());

    /// <summary>
    /// Completa CCC de 9 dígitos (provincia + secuencial) con los 2 dígitos de control.
    /// </summary>
    public static string CompleteCcc(string nineDigitBase)
    {
        var digits = NormalizeDigits(nineDigitBase);
        if (digits.Length != 9)
            throw new ArgumentException("El CCC base debe tener 9 dígitos (provincia + número).");

        var province = int.Parse(digits[..2], CultureInfo.InvariantCulture);
        var sequential = long.Parse(digits[2..], CultureInfo.InvariantCulture);
        var control = ComputeControlDigits(province, sequential);
        return $"{digits}{control:D2}";
    }

    /// <summary>
    /// Deriva un CCC orientativo de 11 dígitos a partir del CIF/NIF (sin garantía de ser el CCC real TGSS).
    /// </summary>
    public static string DeriveOrientativeCccFromTaxId(string taxId)
    {
        var digits = NormalizeDigits(taxId);
        var sequential = digits.Length >= 7
            ? long.Parse(digits[^7..], CultureInfo.InvariantCulture)
            : long.Parse(digits.PadLeft(7, '0'), CultureInfo.InvariantCulture);
        const int unknownProvince = 0;
        var control = ComputeControlDigits(unknownProvince, sequential);
        return $"{unknownProvince:D2}{sequential:D7}{control:D2}";
    }

    public static int ComputeControlDigits(int province, long sequential)
    {
        var baseValue = sequential < 10_000_000
            ? sequential + (long)province * 10_000_000
            : long.Parse($"{province:D2}{sequential}", CultureInfo.InvariantCulture);
        return (int)(baseValue % 97);
    }

    private static bool TryParseAndValidate(string? value, int sequentialLength, out string normalized)
    {
        normalized = NormalizeDigits(value);
        var totalLength = 2 + sequentialLength + 2;
        if (normalized.Length != totalLength || !normalized.All(char.IsDigit))
            return false;

        var province = int.Parse(normalized[..2], CultureInfo.InvariantCulture);
        var sequential = long.Parse(normalized[2..(2 + sequentialLength)], CultureInfo.InvariantCulture);
        var control = int.Parse(normalized[(2 + sequentialLength)..], CultureInfo.InvariantCulture);
        return ComputeControlDigits(province, sequential) == control;
    }
}
