namespace Erp.Application.Common.Interfaces;

public record LimitCheckResult(bool Allowed, string Reason, int Current, int Max);

public interface IPlanLimitService
{
    /// <summary>Counts active users for the tenant and checks against plan MaxUsers.</summary>
    Task<LimitCheckResult> CheckUserLimitAsync(Guid companyId, CancellationToken ct = default);

    /// <summary>Checks currentMonthCount against plan MaxInvoicesPerMonth.</summary>
    Task<LimitCheckResult> CheckInvoiceLimitAsync(Guid companyId, int currentMonthCount, CancellationToken ct = default);
}
