using Erp.Application.Common.Fiscal;
using Erp.Application.Common.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Erp.Modules.Billing.Application.Features.Billing.Queries;

public record GenerateFacturaEResult(byte[] XmlBytes, string FileName);

public record GenerateFacturaEQuery(Guid InvoiceId) : IRequest<GenerateFacturaEResult>;

public class GenerateFacturaEHandler : IRequestHandler<GenerateFacturaEQuery, GenerateFacturaEResult>
{
    private readonly IFacturaEService _facturaE;
    private readonly ITenantContext _tenant;

    public GenerateFacturaEHandler(IFacturaEService facturaE, ITenantContext tenant)
    {
        _facturaE = facturaE;
        _tenant = tenant;
    }

    public async Task<GenerateFacturaEResult> Handle(GenerateFacturaEQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        var (xmlBytes, fileName) = await _facturaE.GenerateAsync(request.InvoiceId, tenantId, ct);
        return new GenerateFacturaEResult(xmlBytes, fileName);
    }
}

public record GenerateSignedFacturaEQuery(Guid InvoiceId) : IRequest<GenerateFacturaEResult>;

public class GenerateSignedFacturaEHandler : IRequestHandler<GenerateSignedFacturaEQuery, GenerateFacturaEResult>
{
    private readonly IFacturaEService _facturaE;
    private readonly ITenantContext _tenant;

    public GenerateSignedFacturaEHandler(IFacturaEService facturaE, ITenantContext tenant)
    {
        _facturaE = facturaE;
        _tenant = tenant;
    }

    public async Task<GenerateFacturaEResult> Handle(GenerateSignedFacturaEQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        var (xmlBytes, fileName) = await _facturaE.GenerateSignedAsync(request.InvoiceId, tenantId, ct);
        return new GenerateFacturaEResult(xmlBytes, fileName);
    }
}

public record SubmitFacturaEFaceCommand(Guid InvoiceId) : IRequest<FaceSubmissionResult>;

public class SubmitFacturaEFaceHandler : IRequestHandler<SubmitFacturaEFaceCommand, FaceSubmissionResult>
{
    private readonly IFacturaEService _facturaE;
    private readonly IFaceSubmissionService _face;
    private readonly ITenantContext _tenant;

    public SubmitFacturaEFaceHandler(
        IFacturaEService facturaE,
        IFaceSubmissionService face,
        ITenantContext tenant)
    {
        _facturaE = facturaE;
        _face = face;
        _tenant = tenant;
    }

    public async Task<FaceSubmissionResult> Handle(SubmitFacturaEFaceCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        var (xmlBytes, fileName) = await _facturaE.GenerateSignedAsync(request.InvoiceId, tenantId, ct);
        return await _face.SubmitAsync(xmlBytes, fileName, ct);
    }
}

public record ValidateFacturaEResult(
    bool Valid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    string FileName);

public record ValidateFacturaEQuery(Guid InvoiceId) : IRequest<ValidateFacturaEResult>;

public record VerifactuSubmissionLogDto(
    string SubmissionType,
    string? EstadoEnvio,
    bool Success,
    bool IsProduction,
    DateTime SubmittedAt);

public record GetVerifactuSubmissionsQuery(Guid InvoiceId) : IRequest<IReadOnlyList<VerifactuSubmissionLogDto>>;

public class GetVerifactuSubmissionsHandler : IRequestHandler<GetVerifactuSubmissionsQuery, IReadOnlyList<VerifactuSubmissionLogDto>>
{
    private readonly IBillingDbContext _ctx;

    public GetVerifactuSubmissionsHandler(IBillingDbContext ctx) => _ctx = ctx;

    public async Task<IReadOnlyList<VerifactuSubmissionLogDto>> Handle(GetVerifactuSubmissionsQuery request, CancellationToken ct)
    {
        return await _ctx.VerifactuSubmissionLogs
            .AsNoTracking()
            .Where(l => l.InvoiceId == request.InvoiceId)
            .OrderByDescending(l => l.SubmittedAt)
            .Select(l => new VerifactuSubmissionLogDto(
                l.SubmissionType, l.EstadoEnvio, l.Success, l.IsProduction, l.SubmittedAt))
            .ToListAsync(ct);
    }
}

public class ValidateFacturaEHandler : IRequestHandler<ValidateFacturaEQuery, ValidateFacturaEResult>
{
    private readonly IFacturaEService _facturaE;
    private readonly ITenantContext _tenant;

    public ValidateFacturaEHandler(IFacturaEService facturaE, ITenantContext tenant)
    {
        _facturaE = facturaE;
        _tenant = tenant;
    }

    public async Task<ValidateFacturaEResult> Handle(ValidateFacturaEQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        var (xmlBytes, fileName) = await _facturaE.GenerateAsync(request.InvoiceId, tenantId, ct);
        var xml = Encoding.UTF8.GetString(xmlBytes);
        var result = FacturaEXmlStructureValidator.Validate(xml);
        return new ValidateFacturaEResult(result.IsValid, result.Errors, result.Warnings, fileName);
    }
}
