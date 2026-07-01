namespace Erp.Application.Common.Interfaces;

/// <summary>
/// Abstraction for sending individual invoice registrations to the AEAT TIKE endpoint (VERI*FACTU, RD 1007/2023).
/// Implemented in Erp.Infrastructure to keep the Application layer free of HTTP/SOAP infrastructure details.
/// </summary>
public interface IVerifactuSubmissionService
{
    /// <summary>
    /// Sends a pre-generated VERI*FACTU XML registration for a single invoice to the AEAT TIKE endpoint.
    /// </summary>
    /// <param name="verifactuXml">XML produced by VerifactuXmlGenerator.</param>
    /// <param name="useProd">True → production endpoint; false → AEAT pre-production (PRE).</param>
    Task<VerifactuSubmitResult> SubmitSingleAsync(
        string verifactuXml,
        bool useProd = false,
        CancellationToken ct = default);
}

/// <summary>Result of a VERI*FACTU TIKE submission.</summary>
public record VerifactuSubmitResult(
    bool   Success,
    string EstadoEnvio,   // "Correcto", "AceptadoConErrores", "Incorrecto", "NetworkError"
    string RawResponse);
