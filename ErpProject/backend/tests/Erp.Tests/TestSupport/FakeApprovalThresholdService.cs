using Erp.Application.Common.Interfaces;

namespace Erp.Tests.TestSupport;

public sealed class FakeApprovalThresholdService(decimal threshold = 0m) : IApprovalThresholdService
{
    public Task<decimal> GetThresholdAsync(Guid companyId, CancellationToken ct = default) =>
        Task.FromResult(threshold);
}
