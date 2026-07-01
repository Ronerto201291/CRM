using OtpNet;
using System.Security.Cryptography;
using System.Text.Json;

namespace Erp.Infrastructure.Services;

/// <summary>
/// TOTP (Time-based One-Time Password) service for 2FA.
/// Uses RFC 6238 standard, compatible with Google Authenticator and Authy.
/// </summary>
public class TotpService : Erp.Application.Common.Interfaces.ITotpService
{
    private const int BackupCodeCount = 8;
    private const int TotpStep = 30;        // 30 second window
    private const int TotpDigits = 6;
    private const int VerifyWindowSteps = 1; // Allow ±1 step for clock skew

    /// <summary>
    /// Generates a new TOTP secret and QR code URI.
    /// </summary>
    public (string secret, string qrCodeUri) GenerateSetup(string email, string issuer = "ErpSaaS")
    {
        var key = KeyGeneration.GenerateRandomKey(20); // 160-bit key
        var base32Secret = Base32Encoding.ToString(key);
        var qrUri = $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(email)}" +
                    $"?secret={base32Secret}&issuer={Uri.EscapeDataString(issuer)}&digits={TotpDigits}&period={TotpStep}";
        return (base32Secret, qrUri);
    }

    /// <summary>
    /// Verifies a TOTP code against a stored secret.
    /// Allows ±1 step window for clock skew.
    /// </summary>
    public bool Verify(string base32Secret, string code)
    {
        if (string.IsNullOrWhiteSpace(base32Secret) || string.IsNullOrWhiteSpace(code))
            return false;

        try
        {
            var key = Base32Encoding.ToBytes(base32Secret);
            var totp = new Totp(key, step: TotpStep, totpSize: TotpDigits);
            return totp.VerifyTotp(code.Trim(), out _, VerificationWindow.RfcSpecifiedNetworkDelay);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Generates 8 one-time backup codes (hex strings).
    /// </summary>
    public List<string> GenerateBackupCodes()
    {
        var codes = new List<string>(BackupCodeCount);
        for (int i = 0; i < BackupCodeCount; i++)
        {
            var bytes = RandomNumberGenerator.GetBytes(5);
            codes.Add(Convert.ToHexStringLower(bytes));
        }
        return codes;
    }

    /// <summary>
    /// Validates and consumes a backup code (removes it from the list).
    /// Returns updated JSON or null if code invalid.
    /// </summary>
    public string? ValidateAndConsumeBackupCode(string? backupCodesJson, string inputCode)
    {
        if (string.IsNullOrEmpty(backupCodesJson)) return null;

        var codes = JsonSerializer.Deserialize<List<string>>(backupCodesJson) ?? new List<string>();
        var normalized = inputCode.Trim().ToLowerInvariant();

        if (!codes.Remove(normalized)) return null;

        return JsonSerializer.Serialize(codes);
    }
}
