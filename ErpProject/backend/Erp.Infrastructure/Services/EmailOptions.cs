using System.ComponentModel.DataAnnotations;

namespace Erp.Infrastructure.Services;

public class EmailOptions
{
    public const string SectionName = "Email";

    [Required] public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = false;   // STARTTLS on 587, SSL on 465
    [Required] public string Username { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
    [Required] public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "ERP SaaS";
    public string AppBaseUrl { get; set; } = "https://app.example.com";

    /// <summary>
    /// URL base del portal público de presupuestos (sin trailing slash).
    /// Ejemplo: "https://app.example.com". Se usa para construir el enlace
    /// que se envía al cliente en el email de presupuesto.
    /// </summary>
    public string PortalBaseUrl { get; set; } = string.Empty;
}
