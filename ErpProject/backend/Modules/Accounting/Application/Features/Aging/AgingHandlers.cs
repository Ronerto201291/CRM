using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using MediatR;

namespace Erp.Modules.Accounting.Application.Features.Aging;

public record GetReceivablesAgingQuery : IRequest<AgingBucketsDto>;
public record GetPayablesAgingQuery : IRequest<AgingBucketsDto>;
public record GetDsoQuery : IRequest<decimal>;
public record GetDpoQuery : IRequest<decimal>;
public record CalculateAgingCommand(string? Type) : IRequest<AgingReportPersistedDto>;

public record AgingReportPersistedDto(
    Guid Id,
    string Status,
    string Message,
    AgingBucketsDto? Receivables,
    AgingBucketsDto? Payables);

public sealed class GetReceivablesAgingHandler : IRequestHandler<GetReceivablesAgingQuery, AgingBucketsDto>
{
    private readonly IAgingDataService _aging;
    private readonly ITenantContext _tenant;

    public GetReceivablesAgingHandler(IAgingDataService aging, ITenantContext tenant)
    {
        _aging = aging;
        _tenant = tenant;
    }

    public Task<AgingBucketsDto> Handle(GetReceivablesAgingQuery request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        return _aging.GetReceivablesAgingAsync(companyId, ct);
    }
}

public sealed class GetPayablesAgingHandler : IRequestHandler<GetPayablesAgingQuery, AgingBucketsDto>
{
    private readonly IAgingDataService _aging;
    private readonly ITenantContext _tenant;

    public GetPayablesAgingHandler(IAgingDataService aging, ITenantContext tenant)
    {
        _aging = aging;
        _tenant = tenant;
    }

    public Task<AgingBucketsDto> Handle(GetPayablesAgingQuery request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        return _aging.GetPayablesAgingAsync(companyId, ct);
    }
}

public sealed class GetDsoHandler : IRequestHandler<GetDsoQuery, decimal>
{
    private readonly IAgingDataService _aging;
    private readonly ITenantContext _tenant;

    public GetDsoHandler(IAgingDataService aging, ITenantContext tenant)
    {
        _aging = aging;
        _tenant = tenant;
    }

    public Task<decimal> Handle(GetDsoQuery request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        return _aging.CalculateDsoAsync(companyId, ct);
    }
}

public sealed class GetDpoHandler : IRequestHandler<GetDpoQuery, decimal>
{
    private readonly IAgingDataService _aging;
    private readonly ITenantContext _tenant;

    public GetDpoHandler(IAgingDataService aging, ITenantContext tenant)
    {
        _aging = aging;
        _tenant = tenant;
    }

    public Task<decimal> Handle(GetDpoQuery request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        return _aging.CalculateDpoAsync(companyId, ct);
    }
}

public sealed class CalculateAgingHandler : IRequestHandler<CalculateAgingCommand, AgingReportPersistedDto>
{
    private readonly IAgingDataService _aging;
    private readonly IAccountingDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CalculateAgingHandler(IAgingDataService aging, IAccountingDbContext ctx, ITenantContext tenant)
    {
        _aging = aging;
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<AgingReportPersistedDto> Handle(CalculateAgingCommand request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        var receivables = await _aging.GetReceivablesAgingAsync(companyId, ct);
        var payables = await _aging.GetPayablesAgingAsync(companyId, ct);

        foreach (var (type, data) in new[] { ("Receivables", receivables), ("Payables", payables) })
        {
            if (!string.IsNullOrEmpty(request.Type)
                && !string.Equals(request.Type, type, StringComparison.OrdinalIgnoreCase))
                continue;

            _ctx.AgingReports.Add(new Domain.Entities.AgingReport
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                ReportDate = DateTime.UtcNow,
                Type = type,
                TotalAmount = data.TotalAmount,
                Current = data.Current,
                Days31To60 = data.Days31To60,
                Days61To90 = data.Days61To90,
                Days91Plus = data.Days91Plus,
                DSO = data.Dso,
                DPO = data.Dpo
            });
        }

        await _ctx.SaveChangesAsync(ct);

        return new AgingReportPersistedDto(
            Guid.NewGuid(),
            "Calculated",
            "Informe de antigüedad calculado desde facturas y gastos reales.",
            receivables,
            payables);
    }
}
