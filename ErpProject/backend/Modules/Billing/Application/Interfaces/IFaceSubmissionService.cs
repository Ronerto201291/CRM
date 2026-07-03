namespace Erp.Modules.Billing.Application.Interfaces;

/// <summary>Envío FacturaE a FACe (B2G). Primer paso: contrato + stub.</summary>
public interface IFaceSubmissionService
{
    bool IsConfigured { get; }

    Task<FaceSubmissionResult> SubmitAsync(byte[] signedXml, string fileName, CancellationToken ct = default);
}

public record FaceSubmissionResult(
    bool Success,
    string Message,
    string? ReferenceId = null,
    int? HttpStatusCode = null,
    string? ResponseBody = null);
