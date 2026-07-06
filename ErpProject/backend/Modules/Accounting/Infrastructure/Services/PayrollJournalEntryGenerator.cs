using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Services;

namespace Erp.Modules.Accounting.Infrastructure.Services;

public sealed class PayrollJournalEntryGenerator : IPayrollJournalEntryGenerator
{
    private readonly AccountingService _accounting;

    public PayrollJournalEntryGenerator(AccountingService accounting) => _accounting = accounting;

    public async Task<Guid> GenerateFromPayrollSettlementAsync(
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
    {
        var entry = await _accounting.GenerateEntryFromPayrollSettlement(
            companyId, payrollSettlementId, year, month,
            totalGross, totalEmployerSocialSecurity, totalEmployeeSocialSecurity,
            totalIrpfWithheld, totalNetPay, accrualDateUtc, ct);
        return entry.Id;
    }
}
