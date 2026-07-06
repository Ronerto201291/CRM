namespace Erp.Modules.Payroll.Application.Services;

/// <summary>Cálculo básico nómina Fase 1 — tablas configurables por plantilla, no convenio oficial.</summary>
public static class PayrollCalculationService
{
    public sealed record Input(
        decimal GrossSalary,
        decimal EmployeeSsRatePercent,
        decimal EmployerSsRatePercent,
        decimal IrpfRatePercent);

    public sealed record Result(
        decimal CommonContingenciesBase,
        decimal EmployeeSocialSecurity,
        decimal EmployerSocialSecurity,
        decimal IrpfBase,
        decimal IrpfRate,
        decimal IrpfWithheld,
        decimal NetPay);

    public static Result Calculate(Input input)
    {
        if (input.GrossSalary < 0)
            throw new ArgumentException("El salario bruto no puede ser negativo.");

        var baseCc = Math.Round(input.GrossSalary, 4);
        var empSs = Math.Round(baseCc * input.EmployeeSsRatePercent / 100m, 4);
        var erSs = Math.Round(baseCc * input.EmployerSsRatePercent / 100m, 4);
        var irpfBase = baseCc;
        var irpfRate = Math.Round(input.IrpfRatePercent, 4);
        var irpfW = Math.Round(irpfBase * irpfRate / 100m, 4);
        var net = Math.Round(baseCc - empSs - irpfW, 4);

        return new Result(baseCc, empSs, erSs, irpfBase, irpfRate, irpfW, net);
    }
}
