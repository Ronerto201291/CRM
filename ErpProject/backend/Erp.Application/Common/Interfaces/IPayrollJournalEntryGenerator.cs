namespace Erp.Application.Common.Interfaces;

/// <summary>
/// Puerto para generar asiento de nómina desde Payroll (ADR-0018 #19c).
/// </summary>
public interface IPayrollJournalEntryGenerator
{
    Task<Guid> GenerateFromPayrollSettlementAsync(
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
        CancellationToken ct = default);
}
