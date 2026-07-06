using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using MediatR;

namespace Erp.Modules.Accounting.Application.Features.Aeat;

public record ListAeatModelsQuery : IRequest<IReadOnlyList<AeatModelDto>>;
public record CreateModelo347Command(int Year) : IRequest<AeatModelDto>;
public record GetModelo347Query(int Year) : IRequest<AeatModelDto?>;
public record ExportModelo347TxtCommand(Guid Id) : IRequest<AeatModelExportResultDto>;
public record CreateModelo111Command(int Year, int Month) : IRequest<AeatModelDto>;
public record CreateModelo200Command(int Year) : IRequest<AeatModelDto>;
public record CreateModelo202Command(int Year) : IRequest<AeatModelDto>;
public record SignAndSubmitAeatModelCommand(Guid Id) : IRequest<AeatModelSubmitResultDto>;

public sealed class ListAeatModelsHandler : IRequestHandler<ListAeatModelsQuery, IReadOnlyList<AeatModelDto>>
{
    private readonly IAeatModelsDataService _service;
    private readonly ITenantContext _tenant;

    public ListAeatModelsHandler(IAeatModelsDataService service, ITenantContext tenant)
    {
        _service = service;
        _tenant = tenant;
    }

    public Task<IReadOnlyList<AeatModelDto>> Handle(ListAeatModelsQuery request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        return _service.ListModelsAsync(companyId, ct);
    }
}

public sealed class CreateModelo347Handler : IRequestHandler<CreateModelo347Command, AeatModelDto>
{
    private readonly IAeatModelsDataService _service;
    private readonly ITenantContext _tenant;

    public CreateModelo347Handler(IAeatModelsDataService service, ITenantContext tenant)
    {
        _service = service;
        _tenant = tenant;
    }

    public Task<AeatModelDto> Handle(CreateModelo347Command request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        return _service.CreateModelo347Async(companyId, request.Year, ct);
    }
}

public sealed class GetModelo347Handler : IRequestHandler<GetModelo347Query, AeatModelDto?>
{
    private readonly IAeatModelsDataService _service;
    private readonly ITenantContext _tenant;

    public GetModelo347Handler(IAeatModelsDataService service, ITenantContext tenant)
    {
        _service = service;
        _tenant = tenant;
    }

    public Task<AeatModelDto?> Handle(GetModelo347Query request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        return _service.GetModelo347Async(companyId, request.Year, ct);
    }
}

public sealed class ExportModelo347TxtHandler : IRequestHandler<ExportModelo347TxtCommand, AeatModelExportResultDto>
{
    private readonly IAeatModelsDataService _service;
    private readonly ITenantContext _tenant;

    public ExportModelo347TxtHandler(IAeatModelsDataService service, ITenantContext tenant)
    {
        _service = service;
        _tenant = tenant;
    }

    public Task<AeatModelExportResultDto> Handle(ExportModelo347TxtCommand request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        return _service.ExportModelo347TxtAsync(companyId, request.Id, ct);
    }
}

public sealed class CreateModelo111Handler : IRequestHandler<CreateModelo111Command, AeatModelDto>
{
    private readonly IAeatModelsDataService _service;
    private readonly ITenantContext _tenant;

    public CreateModelo111Handler(IAeatModelsDataService service, ITenantContext tenant)
    {
        _service = service;
        _tenant = tenant;
    }

    public Task<AeatModelDto> Handle(CreateModelo111Command request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        return _service.CreateModelo111Async(companyId, request.Year, request.Month, ct);
    }
}

public sealed class CreateModelo200Handler : IRequestHandler<CreateModelo200Command, AeatModelDto>
{
    private readonly IAeatModelsDataService _service;
    private readonly ITenantContext _tenant;

    public CreateModelo200Handler(IAeatModelsDataService service, ITenantContext tenant)
    {
        _service = service;
        _tenant = tenant;
    }

    public Task<AeatModelDto> Handle(CreateModelo200Command request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        return _service.CreateModelo200Async(companyId, request.Year, ct);
    }
}

public sealed class CreateModelo202Handler : IRequestHandler<CreateModelo202Command, AeatModelDto>
{
    private readonly IAeatModelsDataService _service;
    private readonly ITenantContext _tenant;

    public CreateModelo202Handler(IAeatModelsDataService service, ITenantContext tenant)
    {
        _service = service;
        _tenant = tenant;
    }

    public Task<AeatModelDto> Handle(CreateModelo202Command request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        return _service.CreateModelo202Async(companyId, request.Year, ct);
    }
}

public sealed class SignAndSubmitAeatModelHandler : IRequestHandler<SignAndSubmitAeatModelCommand, AeatModelSubmitResultDto>
{
    private readonly IAeatModelsDataService _service;
    private readonly ITenantContext _tenant;

    public SignAndSubmitAeatModelHandler(IAeatModelsDataService service, ITenantContext tenant)
    {
        _service = service;
        _tenant = tenant;
    }

    public Task<AeatModelSubmitResultDto> Handle(SignAndSubmitAeatModelCommand request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        return _service.SignAndSubmitAsync(companyId, request.Id, ct);
    }
}
