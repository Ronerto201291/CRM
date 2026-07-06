namespace Erp.Application.Common.Validation;

/// <summary>Validación IBAN (mod-97).</summary>
public static class IbanValidator
{
    public static bool IsValid(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban)) return false;
        var normalized = iban.Replace(" ", "").ToUpperInvariant();
        if (normalized.Length is < 15 or > 34) return false;
        if (!normalized.All(c => char.IsLetterOrDigit(c))) return false;

        var rearranged = normalized[4..] + normalized[..4];
        var numeric = string.Concat(rearranged.Select(c =>
            char.IsLetter(c) ? (c - 'A' + 10).ToString() : c.ToString()));

        var remainder = 0;
        foreach (var ch in numeric)
        {
            remainder = (remainder * 10 + (ch - '0')) % 97;
        }
        return remainder == 1;
    }
}
