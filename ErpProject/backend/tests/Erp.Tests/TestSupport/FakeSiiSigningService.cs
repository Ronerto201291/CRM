using Erp.Infrastructure.Services.Sii;

namespace Erp.Tests.TestSupport;

/// <summary>Mock de firma SII para tests unitarios (sin certificado PKCS#12).</summary>
public sealed class FakeSiiSigningService : ISiiSigningService
{
    public FakeSiiSigningService(bool configured = true, string signedSuffix = "<!-- signed -->")
    {
        IsConfigured = configured;
        SignedSuffix = signedSuffix;
    }

    public bool IsConfigured { get; }
    public string SignedSuffix { get; }

    public string Sign(string xmlContent) => IsConfigured
        ? xmlContent + SignedSuffix
        : throw new InvalidOperationException("SII signing certificate not configured.");
}
