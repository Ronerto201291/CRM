namespace Erp.Application.Common.Interfaces;

/// <summary>
/// Umbral de aprobación por empresa (ADR-0018 #30).
/// <c>0</c> desactiva el workflow: todo se auto-aprueba al enviar.
/// Con umbral &gt; 0, importes estrictamente superiores requieren aprobación manual.
/// </summary>
public interface IApprovalThresholdService
{
    Task<decimal> GetThresholdAsync(Guid companyId, CancellationToken ct = default);
    bool RequiresManualApproval(decimal amount, decimal threshold) =>
        threshold > 0m && amount > threshold;
}
