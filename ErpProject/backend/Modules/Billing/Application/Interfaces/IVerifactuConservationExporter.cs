namespace Erp.Modules.Billing.Application.Interfaces;

public sealed record VerifactuConservationPackage(
    byte[] ZipBytes,
    string FileName,
    int InvoiceCount);

/// <summary>Exporta paquete auditable de registros VERI*FACTU (modalidad no-VERI*FACTU).</summary>
public interface IVerifactuConservationExporter
{
    Task<VerifactuConservationPackage> ExportAsync(
        Guid companyId,
        int year,
        int? month,
        CancellationToken ct = default);
}
