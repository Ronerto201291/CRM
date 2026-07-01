namespace Erp.Modules.Billing.Application.Interfaces;

/// <summary>
/// Generates VERI*FACTU XML for AEAT TIKE registration (RD 1007/2023).
/// Implementation lives in Erp.Infrastructure.
/// </summary>
public interface IVerifactuXmlGenerator
{
    Task<string> GenerateRegistroAsync(Guid companyId, int year, int month, CancellationToken ct = default);
}