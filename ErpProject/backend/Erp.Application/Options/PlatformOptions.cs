namespace Erp.Application.Options;

/// <summary>Configuración de plataforma SaaS (super-admins, etc.).</summary>
public sealed class PlatformOptions
{
    public const string SectionName = "Platform";

    /// <summary>Emails con acceso a operaciones de plataforma (invitar empresas, listar tenants).</summary>
    public string[] SuperAdminEmails { get; set; } = ["admin@devcorp.com"];
}
