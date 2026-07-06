using Erp.Modules.Billing.Application.Interfaces;

namespace Erp.Modules.Billing.Infrastructure.Services;

public class VerifactuChainQuery : IVerifactuChainQuery
{
    private readonly IBillingDbContext _billing;

    public VerifactuChainQuery(IBillingDbContext billing) => _billing = billing;

    public Task<string?> GetLastHuellaBeforeAsync(
        Guid companyId,
        string series,
        int fiscalYear,
        DateTime beforeUtc,
        CancellationToken ct = default)
        => VerifactuChainHelper.GetLastHuellaBeforeAsync(
            _billing, companyId, series, fiscalYear, beforeUtc, ct);
}
