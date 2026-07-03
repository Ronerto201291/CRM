namespace Erp.Modules.Accounting.Application.Interfaces;

public record AgingBucketsDto(
    string Type,
    decimal TotalAmount,
    decimal Current,
    decimal Days31To60,
    decimal Days61To90,
    decimal Days91Plus,
    decimal Dso,
    decimal Dpo);

public interface IAgingDataService
{
    Task<AgingBucketsDto> GetReceivablesAgingAsync(Guid companyId, CancellationToken ct);
    Task<AgingBucketsDto> GetPayablesAgingAsync(Guid companyId, CancellationToken ct);
    Task<decimal> CalculateDsoAsync(Guid companyId, CancellationToken ct);
    Task<decimal> CalculateDpoAsync(Guid companyId, CancellationToken ct);
}
