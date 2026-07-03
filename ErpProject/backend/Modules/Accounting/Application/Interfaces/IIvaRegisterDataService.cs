namespace Erp.Modules.Accounting.Application.Interfaces;

public record IvaRegisterSummaryDto(
    int TotalRecords,
    decimal PurchaseVat,
    decimal SalesVat,
    int PurchaseRecords,
    int SalesRecords,
    int IntraEuCount);

public record IvaRegisterDetailDto(
    string Type,
    int Records,
    decimal TotalVat,
    DateTime? LastExport,
    int IntraEu);

public record RivaExportResultDto(
    Guid Id,
    string FileName,
    string Format,
    int TotalRecords,
    decimal TotalVat,
    string Message,
    string Status);

public record SiiDeclarationDto(
    Guid Id,
    string Status,
    decimal TotalVatOutput,
    decimal TotalVatInput,
    decimal NetVat,
    string Message);

public record IntraEuSummaryDto(
    int TotalOperations,
    decimal TotalAmount,
    IReadOnlyList<string> Countries,
    int TriangularOperations,
    bool ReverseChargeApplied);

public record IvaRegisterLineDto(
    string Name,
    string TaxId,
    decimal BaseAmount,
    decimal VatAmount,
    bool IsIntraEu);

public interface IIvaRegisterDataService
{
    Task<IvaRegisterSummaryDto> GetSummaryAsync(Guid companyId, CancellationToken ct);
    Task<IvaRegisterDetailDto> GetPurchaseRegisterAsync(Guid companyId, CancellationToken ct);
    Task<IvaRegisterDetailDto> GetSalesRegisterAsync(Guid companyId, CancellationToken ct);
    Task<IReadOnlyList<IvaRegisterLineDto>> GetPurchaseLinesAsync(Guid companyId, int limit, CancellationToken ct);
    Task<IReadOnlyList<IvaRegisterLineDto>> GetSalesLinesAsync(Guid companyId, int limit, CancellationToken ct);
    Task<RivaExportResultDto> ExportRivaAsync(Guid companyId, int? year, int? month, CancellationToken ct);
    Task<SiiDeclarationDto> CreateSiiDeclarationAsync(Guid companyId, int year, int month, CancellationToken ct);
    Task<SiiDeclarationDto> SubmitSiiDeclarationAsync(Guid companyId, Guid id, CancellationToken ct);
    Task<IntraEuSummaryDto> GetIntraEuOperationsAsync(Guid companyId, CancellationToken ct);
}
