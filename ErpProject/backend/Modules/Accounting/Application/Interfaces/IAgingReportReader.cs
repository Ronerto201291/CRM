namespace Erp.Modules.Accounting.Application.Interfaces;

public record AgingLineRow(
    Guid Id,
    string Reference,
    string CounterpartyName,
    DateTime ReferenceDate,
    int DaysOutstanding,
    int DaysFromIssue,
    decimal Amount,
    string Status);

public record AgingBucketRow(
    string Type,
    decimal TotalAmount,
    decimal Current,
    decimal Days31To60,
    decimal Days61To90,
    decimal Days91Plus,
    IReadOnlyList<AgingLineRow> Lines);

public record AgingReportData(
    DateTime ReportDate,
    AgingBucketRow Receivables,
    AgingBucketRow Payables,
    decimal DSO,
    decimal DPO,
    string Note);

public interface IAgingReportReader
{
    Task<AgingReportData> GetReportAsync(Guid companyId, DateTime? asOf, CancellationToken ct = default);
}
