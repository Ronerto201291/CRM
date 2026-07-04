using System.Text;
using Erp.Application.Common.Interfaces;
using Erp.Infrastructure.Services.Sii;
using Erp.Modules.Billing.Application.Interfaces;
using MediatR;

namespace Erp.Infrastructure.Features.Sii;

public record SiiXmlFileResult(byte[] Bytes, string FileName, string ContentType = "application/xml");

public record GetSiiEmitidasXmlQuery(int Year, int Month) : IRequest<SiiXmlFileResult>;
public record GetSiiRecibidasXmlQuery(int Year, int Month) : IRequest<SiiXmlFileResult>;

public class GetSiiEmitidasXmlHandler : IRequestHandler<GetSiiEmitidasXmlQuery, SiiXmlFileResult>
{
    private readonly SiiXmlGenerator _generator;
    private readonly ITenantContext _tenant;

    public GetSiiEmitidasXmlHandler(SiiXmlGenerator generator, ITenantContext tenant)
    {
        _generator = generator;
        _tenant = tenant;
    }

    public async Task<SiiXmlFileResult> Handle(GetSiiEmitidasXmlQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var xml = await _generator.GenerateFacturasEmitidasAsync(tenantId, request.Year, request.Month, ct);
        return new SiiXmlFileResult(
            Encoding.UTF8.GetBytes(xml),
            $"SII_FacturasEmitidas_{request.Year}_{request.Month:D2}.xml");
    }
}

public class GetSiiRecibidasXmlHandler : IRequestHandler<GetSiiRecibidasXmlQuery, SiiXmlFileResult>
{
    private readonly SiiXmlGenerator _generator;
    private readonly ITenantContext _tenant;

    public GetSiiRecibidasXmlHandler(SiiXmlGenerator generator, ITenantContext tenant)
    {
        _generator = generator;
        _tenant = tenant;
    }

    public async Task<SiiXmlFileResult> Handle(GetSiiRecibidasXmlQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var xml = await _generator.GenerateFacturasRecibidasAsync(tenantId, request.Year, request.Month, ct);
        return new SiiXmlFileResult(
            Encoding.UTF8.GetBytes(xml),
            $"SII_FacturasRecibidas_{request.Year}_{request.Month:D2}.xml");
    }
}

public record ValidateSiiXmlQuery(int Year, int Month, string? Type) : IRequest<ValidateSiiXmlResult>;

public sealed record ValidateSiiXmlResult(
    string Period,
    string Type,
    bool Valid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    bool SignerConfigured);

public class ValidateSiiXmlHandler : IRequestHandler<ValidateSiiXmlQuery, ValidateSiiXmlResult>
{
    private readonly SiiXmlGenerator _generator;
    private readonly ISiiSigningService _signer;
    private readonly ITenantContext _tenant;

    public ValidateSiiXmlHandler(SiiXmlGenerator generator, ISiiSigningService signer, ITenantContext tenant)
    {
        _generator = generator;
        _signer = signer;
        _tenant = tenant;
    }

    public async Task<ValidateSiiXmlResult> Handle(ValidateSiiXmlQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var invoiceType = request.Type?.ToLower() == "recibidas"
            ? SiiInvoiceType.Recibidas
            : SiiInvoiceType.Emitidas;

        var xml = invoiceType == SiiInvoiceType.Emitidas
            ? await _generator.GenerateFacturasEmitidasAsync(tenantId, request.Year, request.Month, ct)
            : await _generator.GenerateFacturasRecibidasAsync(tenantId, request.Year, request.Month, ct);

        var result = SiiXmlStructureValidator.Validate(xml, invoiceType);

        return new ValidateSiiXmlResult(
            $"{request.Year}-{request.Month:D2}",
            invoiceType.ToString().ToLowerInvariant(),
            result.IsValid,
            result.Errors,
            result.Warnings,
            _signer.IsConfigured);
    }
}

public record PreviewSiiQuery(int Year, int Month) : IRequest<PreviewSiiResult>;

public sealed record PreviewSiiResult(
    string Period,
    string FacturasEmitidasXml,
    string FacturasRecibidasXml,
    string Message);

public class PreviewSiiHandler : IRequestHandler<PreviewSiiQuery, PreviewSiiResult>
{
    private readonly SiiXmlGenerator _generator;
    private readonly ITenantContext _tenant;

    public PreviewSiiHandler(SiiXmlGenerator generator, ITenantContext tenant)
    {
        _generator = generator;
        _tenant = tenant;
    }

    public async Task<PreviewSiiResult> Handle(PreviewSiiQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var emitidas = await _generator.GenerateFacturasEmitidasAsync(tenantId, request.Year, request.Month, ct);
        var recibidas = await _generator.GenerateFacturasRecibidasAsync(tenantId, request.Year, request.Month, ct);

        return new PreviewSiiResult(
            $"{request.Year}-{request.Month:D2}",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(emitidas)),
            Convert.ToBase64String(Encoding.UTF8.GetBytes(recibidas)),
            "Decode from Base64 to review XML before submission");
    }
}

public record SubmitSiiCommand(string? Type, int Year, int Month) : IRequest<SubmitSiiResult>;

public sealed record SubmitSiiResult(bool Success, string? Estado, string Period, string? Error);

public class SubmitSiiHandler : IRequestHandler<SubmitSiiCommand, SubmitSiiResult>
{
    private readonly SiiXmlGenerator _generator;
    private readonly ISiiSigningService _signer;
    private readonly SiiSubmissionService _submission;
    private readonly ITenantContext _tenant;

    public SubmitSiiHandler(
        SiiXmlGenerator generator,
        ISiiSigningService signer,
        SiiSubmissionService submission,
        ITenantContext tenant)
    {
        _generator = generator;
        _signer = signer;
        _submission = submission;
        _tenant = tenant;
    }

    public async Task<SubmitSiiResult> Handle(SubmitSiiCommand request, CancellationToken ct)
    {
        if (!_signer.IsConfigured)
            throw new InvalidOperationException("SII signing certificate not configured.");

        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var invoiceType = request.Type?.ToLower() == "recibidas"
            ? SiiInvoiceType.Recibidas
            : SiiInvoiceType.Emitidas;

        var xml = invoiceType == SiiInvoiceType.Emitidas
            ? await _generator.GenerateFacturasEmitidasAsync(tenantId, request.Year, request.Month, ct)
            : await _generator.GenerateFacturasRecibidasAsync(tenantId, request.Year, request.Month, ct);

        var signedXml = _signer.Sign(xml);
        var result = await _submission.SubmitAsync(signedXml, invoiceType, ct);

        return new SubmitSiiResult(
            result.Success,
            result.Estado,
            $"{request.Year}-{request.Month:D2}",
            result.Success ? null : result.RawResponse);
    }
}

public record GetVerifactuXmlQuery(int Year, int Month) : IRequest<SiiXmlFileResult>;

public class GetVerifactuXmlHandler : IRequestHandler<GetVerifactuXmlQuery, SiiXmlFileResult>
{
    private readonly IVerifactuXmlGenerator _generator;
    private readonly ITenantContext _tenant;

    public GetVerifactuXmlHandler(IVerifactuXmlGenerator generator, ITenantContext tenant)
    {
        _generator = generator;
        _tenant = tenant;
    }

    public async Task<SiiXmlFileResult> Handle(GetVerifactuXmlQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var xml = await _generator.GenerateRegistroAsync(tenantId, request.Year, request.Month, ct);
        return new SiiXmlFileResult(
            Encoding.UTF8.GetBytes(xml),
            $"Verifactu_{request.Year}_{request.Month:D2}.xml");
    }
}

public record SubmitVerifactuCommand(int Year, int Month, bool UseProd = false) : IRequest<SubmitVerifactuResult>;

public sealed record SubmitVerifactuResult(bool Success, string? EstadoEnvio, string Period, string? Error);

public class SubmitVerifactuHandler : IRequestHandler<SubmitVerifactuCommand, SubmitVerifactuResult>
{
    private readonly IVerifactuXmlGenerator _generator;
    private readonly VerifactuSubmissionService _submission;
    private readonly ITenantContext _tenant;

    public SubmitVerifactuHandler(
        IVerifactuXmlGenerator generator,
        VerifactuSubmissionService submission,
        ITenantContext tenant)
    {
        _generator = generator;
        _submission = submission;
        _tenant = tenant;
    }

    public async Task<SubmitVerifactuResult> Handle(SubmitVerifactuCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var xml = await _generator.GenerateRegistroAsync(tenantId, request.Year, request.Month, ct);
        var result = await _submission.SubmitAsync(xml, request.UseProd, ct);

        return new SubmitVerifactuResult(
            result.Success,
            result.EstadoEnvio,
            $"{request.Year}-{request.Month:D2}",
            result.Success ? null : result.RawResponse);
    }
}
