using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using MediatR;

namespace Erp.Modules.Accounting.Application.Features.Export;

public record ExportModelo303XmlQuery(int Year, int Quarter) : IRequest<FiscalCsvExportResult>;

public class ExportModelo303XmlHandler : IRequestHandler<ExportModelo303XmlQuery, FiscalCsvExportResult>
{
    private readonly IModelo303XmlExporter _exporter;
    private readonly ITenantContext _tenant;

    public ExportModelo303XmlHandler(IModelo303XmlExporter exporter, ITenantContext tenant)
    {
        _exporter = exporter;
        _tenant = tenant;
    }

    public async Task<FiscalCsvExportResult> Handle(ExportModelo303XmlQuery request, CancellationToken ct)
    {
        if (request.Quarter < 1 || request.Quarter > 4)
            throw new ArgumentException("q must be 1–4 (quarter)");

        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        return await _exporter.ExportAsync(tenantId, request.Year, request.Quarter, ct);
    }
}

public record ExportModelo200XmlQuery(int Year) : IRequest<FiscalCsvExportResult>;

public class ExportModelo200XmlHandler : IRequestHandler<ExportModelo200XmlQuery, FiscalCsvExportResult>
{
    private readonly IFiscalSkeletonXmlExporter _exporter;
    private readonly ITenantContext _tenant;

    public ExportModelo200XmlHandler(IFiscalSkeletonXmlExporter exporter, ITenantContext tenant)
    {
        _exporter = exporter;
        _tenant = tenant;
    }

    public async Task<FiscalCsvExportResult> Handle(ExportModelo200XmlQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        return await _exporter.ExportModelo200Async(tenantId, request.Year, ct);
    }
}

public record ExportModelo202XmlQuery(int Year, int Period) : IRequest<FiscalCsvExportResult>;

public class ExportModelo202XmlHandler : IRequestHandler<ExportModelo202XmlQuery, FiscalCsvExportResult>
{
    private readonly IFiscalSkeletonXmlExporter _exporter;
    private readonly ITenantContext _tenant;

    public ExportModelo202XmlHandler(IFiscalSkeletonXmlExporter exporter, ITenantContext tenant)
    {
        _exporter = exporter;
        _tenant = tenant;
    }

    public async Task<FiscalCsvExportResult> Handle(ExportModelo202XmlQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        return await _exporter.ExportModelo202Async(tenantId, request.Year, request.Period, ct);
    }
}

public record ExportModelo390XmlQuery(int Year) : IRequest<FiscalCsvExportResult>;

public class ExportModelo390XmlHandler : IRequestHandler<ExportModelo390XmlQuery, FiscalCsvExportResult>
{
    private readonly IModelo390XmlExporter _exporter;
    private readonly ITenantContext _tenant;

    public ExportModelo390XmlHandler(IModelo390XmlExporter exporter, ITenantContext tenant)
    {
        _exporter = exporter;
        _tenant = tenant;
    }

    public async Task<FiscalCsvExportResult> Handle(ExportModelo390XmlQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        return await _exporter.ExportAsync(tenantId, request.Year, ct);
    }
}
