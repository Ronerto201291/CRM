using Erp.Application.Common.Interfaces;

namespace Erp.Tests.TestSupport;

public sealed class FakePlanLimitService : IPlanLimitService
{
    private readonly LimitCheckResult _invoiceResult;
    private readonly LimitCheckResult _userResult;

    public FakePlanLimitService(
        LimitCheckResult? invoiceResult = null,
        LimitCheckResult? userResult = null)
    {
        _invoiceResult = invoiceResult ?? new LimitCheckResult(true, string.Empty, 0, 100);
        _userResult = userResult ?? new LimitCheckResult(true, string.Empty, 0, 100);
    }

    public Task<LimitCheckResult> CheckUserLimitAsync(Guid companyId, CancellationToken ct = default)
        => Task.FromResult(_userResult);

    public Task<LimitCheckResult> CheckInvoiceLimitAsync(Guid companyId, int currentMonthCount, CancellationToken ct = default)
        => Task.FromResult(_invoiceResult);
}
