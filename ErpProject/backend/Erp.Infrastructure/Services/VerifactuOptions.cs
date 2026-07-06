using System.ComponentModel.DataAnnotations;

namespace Erp.Infrastructure.Services;

/// <summary>Modo de cumplimiento VERI*FACTU (RD 1007/2023).</summary>
public enum VerifactuSubmissionMode
{
    /// <summary>Remisión de registros a AEAT en tiempo real (VERI*FACTU).</summary>
    Verifactu = 0,
    /// <summary>Registro local sin remisión en expedición (no VERI*FACTU).</summary>
    LocalOnly = 1
}

/// <summary>
/// Strongly-typed options for VERI*FACTU configuration (RD 1007/2023).
/// Validated at startup — the application will REFUSE to start if NifSoftware
/// is missing, blank, or uses the reserved dummy value "B00000000".
///
/// Required environment variables (or appsettings overrides):
///   Verifactu__NifSoftware       — NIF/CIF del fabricante del software
///   Verifactu__NombreSoftware    — Nombre del software
///   Verifactu__IdSistema         — Identificador único del sistema
///   Verifactu__Version           — Versión del software (e.g. "1.0.0")
///   Verifactu__NumeroInstalacion — Número de instalación por cliente (e.g. "1")
/// </summary>
public sealed class VerifactuOptions
{
    public const string Section = "Verifactu";

    /// <summary>NIF of the software manufacturer (fabricante del software).</summary>
    [Required(ErrorMessage = "Verifactu:NifSoftware es obligatorio.")]
    public string NifSoftware { get; set; } = string.Empty;

    /// <summary>Commercial name of the ERP software.</summary>
    [Required(ErrorMessage = "Verifactu:NombreSoftware es obligatorio.")]
    public string NombreSoftware { get; set; } = string.Empty;

    /// <summary>Unique system identifier (e.g. "MYERP-1").</summary>
    [Required(ErrorMessage = "Verifactu:IdSistema es obligatorio.")]
    public string IdSistema { get; set; } = string.Empty;

    /// <summary>Software version string.</summary>
    [Required]
    public string Version { get; set; } = "1.0";

    /// <summary>Per-installation number. Must be unique per Verifactu:NifSoftware.</summary>
    [Required]
    public string NumeroInstalacion { get; set; } = "1";

    /// <summary>
    /// Si es true, envía a www1.agenciatributaria.gob.es (producción).
    /// Si es false (por defecto), envía a prewww1.aeat.es (PRE/homologación).
    /// </summary>
    public bool UseProduction { get; set; } = false;

    /// <summary>
    /// Verifactu = remisión TIKE en tiempo real; LocalOnly = registro local sin envío AEAT.
    /// </summary>
    public VerifactuSubmissionMode SubmissionMode { get; set; } = VerifactuSubmissionMode.Verifactu;

    // ── Known dummy / placeholder values that must never reach production ──
    private static readonly HashSet<string> FictitiousNifs =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "B00000000", "A00000000", "00000000T", "99999999R",
            "CHANGE_ME", "PLACEHOLDER"
        };

    /// <summary>
    /// Validates that NifSoftware is not blank and not one of the known dummy values.
    /// Called by VerifactuOptionsValidator at startup.
    /// </summary>
    public void AssertValid()
    {
        if (string.IsNullOrWhiteSpace(NifSoftware))
            throw new InvalidOperationException(
                "VERIFACTU ERROR: Verifactu:NifSoftware no está configurado. " +
                "Configure el NIF del fabricante del software en las variables de entorno " +
                "o en appsettings.json para producción.");

        if (FictitiousNifs.Contains(NifSoftware.Trim()))
            throw new InvalidOperationException(
                $"VERIFACTU ERROR: Verifactu:NifSoftware=\"{NifSoftware}\" es un valor ficticio de prueba. " +
                "Configure el NIF real del fabricante del software antes de usar Verifactu en producción. " +
                "Esta validación impide que se generen facturas con NIF ficticio en producción.");

        if (string.IsNullOrWhiteSpace(NombreSoftware))
            throw new InvalidOperationException(
                "VERIFACTU ERROR: Verifactu:NombreSoftware no está configurado.");

        if (string.IsNullOrWhiteSpace(IdSistema))
            throw new InvalidOperationException(
                "VERIFACTU ERROR: Verifactu:IdSistema no está configurado.");
    }
}
