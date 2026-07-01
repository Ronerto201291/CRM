namespace Erp.Application.Common.Interfaces;

/// <summary>
/// Validates EU VAT numbers via the official EU VIES SOAP API.
/// VIES = VAT Information Exchange System (art. 31 Reg. EU 904/2010).
/// </summary>
public interface IViesService
{
    /// <summary>
    /// Validates a VAT number with the VIES service.
    /// </summary>
    /// <param name="countryCode">2-letter ISO country code, e.g. "FR", "DE".</param>
    /// <param name="vatNumber">VAT number without country prefix, e.g. "12345678901".</param>
    Task<ViesValidationResult> ValidateAsync(
        string countryCode, string vatNumber, CancellationToken ct = default);
}

public sealed record ViesValidationResult(
    bool   IsValid,
    string CountryCode,
    string VatNumber,
    string? Name,
    string? Address,
    string? RequestDate,
    string? ErrorMessage);
