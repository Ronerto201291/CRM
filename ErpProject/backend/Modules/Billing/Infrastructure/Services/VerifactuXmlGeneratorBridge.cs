using Erp.Application.Common.Interfaces;
using Erp.Infrastructure.Services;
using Erp.Infrastructure.Services.Sii;
using Erp.Modules.Billing.Application.Interfaces;

namespace Erp.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Bridge: implements IVerifactuXmlGenerator (Billing Application layer)
/// by delegating to VerifactuXmlGenerator (Infrastructure layer).
/// Registered in AddBillingInfrastructure so that Billing Application
/// stays free of Infrastructure concerns.
/// </summary>
public class VerifactuXmlGeneratorBridge : IVerifactuXmlGenerator
{
    private readonly VerifactuXmlGenerator _inner;

    public VerifactuXmlGeneratorBridge(
        IBillingDbContext billing,
        IApplicationDbContext app,
        IVerifactuService verifactu,
        Microsoft.Extensions.Options.IOptions<VerifactuOptions> options)
    {
        _inner = new VerifactuXmlGenerator(billing, app, verifactu, options);
    }

    public Task<string> GenerateRegistroAsync(Guid companyId, int year, int month, CancellationToken ct = default)
        => _inner.GenerateRegistroAsync(companyId, year, month, ct);
}