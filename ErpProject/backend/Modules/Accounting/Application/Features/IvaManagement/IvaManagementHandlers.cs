using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using MediatR;

namespace Erp.Modules.Accounting.Application.Features.IvaManagement;

public record GetIvaRegisterSummaryQuery : IRequest<IvaRegisterSummaryDto>;
public record GetPurchaseIvaRegisterQuery : IRequest<IvaRegisterDetailDto>;
public record GetSalesIvaRegisterQuery : IRequest<IvaRegisterDetailDto>;
public record ExportRivaCommand(int? Year, int? Month) : IRequest<RivaExportResultDto>;
public record CreateSiiDeclarationCommand(int Year, int Month) : IRequest<SiiDeclarationDto>;
public record SubmitSiiDeclarationCommand(Guid Id) : IRequest<SiiDeclarationDto>;
public record GetIntraEuOperationsQuery : IRequest<IntraEuSummaryDto>;

public sealed class GetIvaRegisterSummaryHandler : IRequestHandler<GetIvaRegisterSummaryQuery, IvaRegisterSummaryDto>
{
    private readonly IIvaRegisterDataService _service;
    private readonly ITenantContext _tenant;

    public GetIvaRegisterSummaryHandler(IIvaRegisterDataService service, ITenantContext tenant)
    {
        _service = service;
        _tenant = tenant;
    }

    public Task<IvaRegisterSummaryDto> Handle(GetIvaRegisterSummaryQuery request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        return _service.GetSummaryAsync(companyId, ct);
    }
}

public sealed class GetPurchaseIvaRegisterHandler : IRequestHandler<GetPurchaseIvaRegisterQuery, IvaRegisterDetailDto>
{
    private readonly IIvaRegisterDataService _service;
    private readonly ITenantContext _tenant;

    public GetPurchaseIvaRegisterHandler(IIvaRegisterDataService service, ITenantContext tenant)
    {
        _service = service;
        _tenant = tenant;
    }

    public Task<IvaRegisterDetailDto> Handle(GetPurchaseIvaRegisterQuery request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        return _service.GetPurchaseRegisterAsync(companyId, ct);
    }
}

public sealed class GetSalesIvaRegisterHandler : IRequestHandler<GetSalesIvaRegisterQuery, IvaRegisterDetailDto>
{
    private readonly IIvaRegisterDataService _service;
    private readonly ITenantContext _tenant;

    public GetSalesIvaRegisterHandler(IIvaRegisterDataService service, ITenantContext tenant)
    {
        _service = service;
        _tenant = tenant;
    }

    public Task<IvaRegisterDetailDto> Handle(GetSalesIvaRegisterQuery request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        return _service.GetSalesRegisterAsync(companyId, ct);
    }
}

public sealed class ExportRivaHandler : IRequestHandler<ExportRivaCommand, RivaExportResultDto>
{
    private readonly IIvaRegisterDataService _service;
    private readonly ITenantContext _tenant;

    public ExportRivaHandler(IIvaRegisterDataService service, ITenantContext tenant)
    {
        _service = service;
        _tenant = tenant;
    }

    public Task<RivaExportResultDto> Handle(ExportRivaCommand request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        return _service.ExportRivaAsync(companyId, request.Year, request.Month, ct);
    }
}

public sealed class CreateSiiDeclarationHandler : IRequestHandler<CreateSiiDeclarationCommand, SiiDeclarationDto>
{
    private readonly IIvaRegisterDataService _service;
    private readonly ITenantContext _tenant;

    public CreateSiiDeclarationHandler(IIvaRegisterDataService service, ITenantContext tenant)
    {
        _service = service;
        _tenant = tenant;
    }

    public Task<SiiDeclarationDto> Handle(CreateSiiDeclarationCommand request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        return _service.CreateSiiDeclarationAsync(companyId, request.Year, request.Month, ct);
    }
}

public sealed class SubmitSiiDeclarationHandler : IRequestHandler<SubmitSiiDeclarationCommand, SiiDeclarationDto>
{
    private readonly IIvaRegisterDataService _service;
    private readonly ITenantContext _tenant;

    public SubmitSiiDeclarationHandler(IIvaRegisterDataService service, ITenantContext tenant)
    {
        _service = service;
        _tenant = tenant;
    }

    public Task<SiiDeclarationDto> Handle(SubmitSiiDeclarationCommand request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        return _service.SubmitSiiDeclarationAsync(companyId, request.Id, ct);
    }
}

public sealed class GetIntraEuOperationsHandler : IRequestHandler<GetIntraEuOperationsQuery, IntraEuSummaryDto>
{
    private readonly IIvaRegisterDataService _service;
    private readonly ITenantContext _tenant;

    public GetIntraEuOperationsHandler(IIvaRegisterDataService service, ITenantContext tenant)
    {
        _service = service;
        _tenant = tenant;
    }

    public Task<IntraEuSummaryDto> Handle(GetIntraEuOperationsQuery request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        return _service.GetIntraEuOperationsAsync(companyId, ct);
    }
}
