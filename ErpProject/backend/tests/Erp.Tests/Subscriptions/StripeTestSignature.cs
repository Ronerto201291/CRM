using System.Security.Cryptography;
using System.Text;

namespace Erp.Tests.Subscriptions;

/// <summary>
/// Genera cabecera Stripe-Signature válida para tests (HMAC-SHA256, compatible con EventUtility.ConstructEvent).
/// </summary>
internal static class StripeTestSignature
{
    public static string GenerateHeader(string payload, string webhookSecret, long? timestamp = null)
    {
        var ts = timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signedPayload = $"{ts}.{payload}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhookSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
        var signature = BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        return $"t={ts},v1={signature}";
    }
}
