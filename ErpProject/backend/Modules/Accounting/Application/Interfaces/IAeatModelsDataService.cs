namespace Erp.Modules.Accounting.Application.Interfaces;

public record AeatModelDto(
    Guid Id,
    string Type,
    int Year,
    int? Month,
    string Status,
    int TotalRecords,
    decimal TotalAmount,
    string? Message = null);

public record AeatModelSubmitResultDto(
    Guid Id,
    string Status,
    string SubmissionReference,
    DateTime SubmissionDate,
    string Message);

public record AeatModelExportResultDto(
    Guid Id,
    string FileName,
    string Format,
    string Message);

public interface IAeatModelsDataService
{
    Task<AeatModelDto> CreateModelo347Async(Guid companyId, int year, CancellationToken ct);
    Task<AeatModelDto?> GetModelo347Async(Guid companyId, int year, CancellationToken ct);
    Task<AeatModelExportResultDto> ExportModelo347TxtAsync(Guid companyId, Guid id, CancellationToken ct);
    Task<AeatModelDto> CreateModelo111Async(Guid companyId, int year, int month, CancellationToken ct);
    Task<AeatModelDto> CreateModelo200Async(Guid companyId, int year, CancellationToken ct);
    Task<AeatModelDto> CreateModelo202Async(Guid companyId, int year, CancellationToken ct);
    Task<AeatModelSubmitResultDto> SignAndSubmitAsync(Guid companyId, Guid id, CancellationToken ct);
}
