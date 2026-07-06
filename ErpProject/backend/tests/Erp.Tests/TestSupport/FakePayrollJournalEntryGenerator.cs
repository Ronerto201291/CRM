using Erp.Application.Common.Interfaces;

namespace Erp.Tests.TestSupport;

public sealed class FakePayrollJournalEntryGenerator : IPayrollJournalEntryGenerator
{
    public Guid NextEntryId { get; set; } = Guid.NewGuid();

    public Task<Guid> GenerateFromPayrollSettlementAsync(
        Guid companyId,
        Guid payrollSettlementId,
        int year,
        int month,
        decimal totalGross,
        decimal totalEmployerSocialSecurity,
        decimal totalEmployeeSocialSecurity,
        decimal totalIrpfWithheld,
        decimal totalNetPay,
        DateTime accrualDateUtc,
        CancellationToken ct = default)
        => Task.FromResult(NextEntryId);
}
