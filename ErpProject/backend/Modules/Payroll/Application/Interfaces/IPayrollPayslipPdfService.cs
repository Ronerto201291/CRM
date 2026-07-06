namespace Erp.Modules.Payroll.Application.Interfaces;

public record PayslipCompanyInfo(string Name, string TaxId, string? Address);

public record PayslipEmployeeInfo(
    string FullName,
    string TaxId,
    string? SocialSecurityNumber);

public record PayslipLineInfo(
    int Year,
    int Month,
    decimal GrossSalary,
    decimal CommonContingenciesBase,
    decimal EmployeeSocialSecurity,
    decimal EmployerSocialSecurity,
    decimal IrpfBase,
    decimal IrpfRate,
    decimal IrpfWithheld,
    decimal NetPay,
    string Disclaimer);

public record PayrollPayslipPdfData(
    PayslipCompanyInfo Company,
    PayslipEmployeeInfo Employee,
    PayslipLineInfo Line);

public interface IPayrollPayslipPdfService
{
    byte[] Generate(PayrollPayslipPdfData data);
}
