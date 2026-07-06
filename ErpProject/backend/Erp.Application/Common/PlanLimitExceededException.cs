using Erp.Application.Common.Interfaces;

namespace Erp.Application.Common;

public class PlanLimitExceededException(LimitCheckResult check) : Exception(check.Reason)
{
    public LimitCheckResult Check { get; } = check;
}
