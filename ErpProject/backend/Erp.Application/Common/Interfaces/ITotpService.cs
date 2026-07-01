namespace Erp.Application.Common.Interfaces;

/// <summary>
/// Abstraction for TOTP (Time-based One-Time Password) operations.
/// Defined in Application layer so handlers don't depend on Infrastructure.
/// Implemented by TotpService in Erp.Infrastructure.
/// </summary>
public interface ITotpService
{
    /// <summary>Generates a new TOTP secret and otpauth:// QR URI.</summary>
    (string secret, string qrCodeUri) GenerateSetup(string email, string issuer = "ErpSaaS");

    /// <summary>Verifies a 6-digit TOTP code against a stored base32 secret. Allows ±1 step for clock skew.</summary>
    bool Verify(string base32Secret, string code);

    /// <summary>Generates 8 one-time backup codes (hex strings).</summary>
    List<string> GenerateBackupCodes();

    /// <summary>
    /// Validates and consumes a backup code (removes it from the JSON list).
    /// Returns updated JSON, or null if the code was not found.
    /// </summary>
    string? ValidateAndConsumeBackupCode(string? backupCodesJson, string inputCode);
}
