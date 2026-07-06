namespace Erp.Modules.Accounting.Infrastructure.Services;

public static class EuVatHelper
{
    public static string GetCountryCode(string? vatNumber)
    {
        if (string.IsNullOrWhiteSpace(vatNumber) || vatNumber.Length < 2)
            return "ZZ";
        var prefix = vatNumber[..2].ToUpperInvariant();
        return char.IsLetter(prefix[0]) && char.IsLetter(prefix[1]) ? prefix : "ES";
    }

    public static string? StripCountryPrefix(string? vatNumber)
    {
        if (string.IsNullOrWhiteSpace(vatNumber) || vatNumber.Length < 2) return vatNumber;
        var prefix = vatNumber[..2].ToUpperInvariant();
        return char.IsLetter(prefix[0]) && char.IsLetter(prefix[1])
            ? vatNumber[2..]
            : vatNumber;
    }
}
