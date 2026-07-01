namespace Erp.Application.Common.Interfaces;

/// <summary>
/// Abstraction for Verifactu processing (RD 1007/2023).
/// Implemented by VerifactuService in Infrastructure.
/// </summary>
public interface IVerifactuService
{
    /// <summary>
    /// Computes the Verifactu Huella (hash) and QR validation URL for an invoice.
    /// </summary>
    /// <param name="nifEmisor">NIF of the issuing company.</param>
    /// <param name="numSerieFactura">Invoice number (e.g. "A-2026-000001").</param>
    /// <param name="fechaExpedicion">Invoice issue date.</param>
    /// <param name="tipoFactura">Invoice type code: F1 (normal), R1 (rectificativa), etc.</param>
    /// <param name="cuotaTotal">Total VAT amount.</param>
    /// <param name="importeTotal">Invoice grand total (inc. VAT).</param>
    /// <param name="huellaAnterior">Huella of the previous invoice in the chain (null for first).</param>
    /// <param name="numeroRegistro">Sequential registration number (SequenceNumber).</param>
    /// <param name="fechaHoraHuella">Timestamp when the hash is computed (LockedAt).</param>
    /// <returns>(Huella hex string, QR URL string)</returns>
    (string Huella, string QrUrl) Compute(
        string nifEmisor,
        string numSerieFactura,
        DateOnly fechaExpedicion,
        string tipoFactura,
        decimal cuotaTotal,
        decimal importeTotal,
        string? huellaAnterior,
        int numeroRegistro,
        DateTimeOffset fechaHoraHuella);
}
