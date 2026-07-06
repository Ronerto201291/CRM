namespace Erp.Application.Options;

/// <summary>Web Push VAPID (#42). Deshabilitado por defecto; email sigue como fallback.</summary>
public sealed class WebPushOptions
{
    public const string SectionName = "WebPush";

    public bool Enabled { get; set; }
    public string VapidPublicKey { get; set; } = "";
    public string VapidPrivateKey { get; set; } = "";
    public string VapidSubject { get; set; } = "mailto:noreply@example.com";
}
