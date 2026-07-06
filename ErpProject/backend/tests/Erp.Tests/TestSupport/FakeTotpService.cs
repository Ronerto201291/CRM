using Erp.Application.Common.Interfaces;
using System.Text.Json;

namespace Erp.Tests.TestSupport;

/// <summary>TOTP fake configurable para tests de auth 2FA.</summary>
public sealed class FakeTotpService : ITotpService
{
    public string FixedSecret { get; set; } = "JBSWY3DPEHPK3PXP";
    public string FixedQrUri { get; set; } = "otpauth://totp/ErpSaaS:test@test.local?secret=JBSWY3DPEHPK3PXP";
    public List<string> FixedBackupCodes { get; set; } = ["backup-aaaa", "backup-bbbb"];
    public string ValidCode { get; set; } = "123456";
    public bool VerifyResult { get; set; } = true;

    public (string secret, string qrCodeUri) GenerateSetup(string email, string issuer = "ErpSaaS")
        => (FixedSecret, FixedQrUri);

    public bool Verify(string base32Secret, string code)
        => VerifyResult && code == ValidCode;

    public List<string> GenerateBackupCodes() => [.. FixedBackupCodes];

    public string? ValidateAndConsumeBackupCode(string? backupCodesJson, string inputCode)
    {
        if (string.IsNullOrEmpty(backupCodesJson)) return null;
        var codes = JsonSerializer.Deserialize<List<string>>(backupCodesJson) ?? [];
        if (!codes.Remove(inputCode)) return null;
        return JsonSerializer.Serialize(codes);
    }
}
