using Erp.Application.Common.Validation;

namespace Erp.Modules.Payroll.Application.Features.Exports;

internal static class PayrollRedExportValidator
{
    internal static void ValidatePeriod(int year, int month)
    {
        PayrollExportValidators.ValidateMonth(month);
        if (year is < 2000 or > 2099)
            throw new ArgumentException("Year debe estar entre 2000 y 2099.");
    }

    internal static void ValidateCompany(string taxId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("La empresa debe tener razón social.");

        if (!SpanishTaxIdValidator.IsValid(taxId))
            throw new ArgumentException("NIF/CIF de empresa no válido para export RED.");
    }

    internal static void ValidateWorkerLines(IReadOnlyList<PayrollExportLine> lines)
    {
        foreach (var line in lines)
        {
            if (!SpanishTaxIdValidator.IsValid(line.TaxId))
                throw new ArgumentException($"NIF no válido del trabajador {line.FullName}.");

            if (string.IsNullOrWhiteSpace(line.SocialSecurityNumber))
                throw new ArgumentException($"NAF obligatorio para RED: {line.FullName}.");

            if (!SpanishSocialSecurityNumberValidator.IsValidNaf(line.SocialSecurityNumber))
                throw new ArgumentException($"NAF no válido (módulo 97) para {line.FullName}.");
        }
    }

    internal static string ResolveEmployerCcc(string companyTaxId)
    {
        var digits = SpanishSocialSecurityNumberValidator.NormalizeDigits(companyTaxId);
        if (digits.Length >= 11 && SpanishSocialSecurityNumberValidator.IsValidCcc(digits[..11]))
            return digits[..11];

        if (digits.Length == 9)
            return SpanishSocialSecurityNumberValidator.CompleteCcc(digits);

        return SpanishSocialSecurityNumberValidator.DeriveOrientativeCccFromTaxId(companyTaxId);
    }
}
