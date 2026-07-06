using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using MediatR;

namespace Erp.Modules.Accounting.Application.Features.Aging;

public record GetAgingReportQuery(DateTime? AsOf = null) : IRequest<AgingReportDto>;

public record AgingReportDto(
    DateTime ReportDate,
    AgingBucketDto Receivables,
    AgingBucketDto Payables,
    decimal DSO,
    decimal DPO,
    string Note);

public record AgingBucketDto(
    string Type,
    decimal TotalAmount,
    decimal Current,
    decimal Days31To60,
    decimal Days61To90,
    decimal Days91Plus,
    IReadOnlyList<AgingLineDto> Lines);

public record AgingLineDto(
    Guid Id,
    string Reference,
    string CounterpartyName,
    DateTime ReferenceDate,
    int DaysOutstanding,
    decimal Amount,
    string Status);

public class GetAgingReportHandler : IRequestHandler<GetAgingReportQuery, AgingReportDto>
{
    private readonly IAgingReportReader _reader;
    private readonly ITenantContext _tenant;

    public GetAgingReportHandler(IAgingReportReader reader, ITenantContext tenant)
    {
        _reader = reader;
        _tenant = tenant;
    }

    public async Task<AgingReportDto> Handle(GetAgingReportQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        var data = await _reader.GetReportAsync(tenantId, request.AsOf, ct);

        static AgingLineDto MapLine(AgingLineRow r) => new(
            r.Id, r.Reference, r.CounterpartyName, r.ReferenceDate,
            r.DaysOutstanding, r.Amount, r.Status);

        static AgingBucketDto MapBucket(AgingBucketRow b) => new(
            b.Type, b.TotalAmount, b.Current, b.Days31To60, b.Days61To90, b.Days91Plus,
            b.Lines.Select(MapLine).ToList());

        return new AgingReportDto(
            data.ReportDate,
            MapBucket(data.Receivables),
            MapBucket(data.Payables),
            data.DSO,
            data.DPO,
            data.Note);
    }
}
