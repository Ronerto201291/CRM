using Erp.Modules.Payroll.Application.Services;
using Xunit;

namespace Erp.Tests.Payroll;

public class PayrollCalculationServiceTests
{
    [Fact]
    public void Calculate_StandardRates_ProducesExpectedNet()
    {
        var result = PayrollCalculationService.Calculate(new PayrollCalculationService.Input(
            GrossSalary: 1500m,
            EmployeeSsRatePercent: 6.35m,
            EmployerSsRatePercent: 30m,
            IrpfRatePercent: 15m));

        Assert.Equal(1500m, result.CommonContingenciesBase);
        Assert.Equal(95.25m, result.EmployeeSocialSecurity);
        Assert.Equal(450m, result.EmployerSocialSecurity);
        Assert.Equal(225m, result.IrpfWithheld);
        Assert.Equal(1179.75m, result.NetPay);
    }

    [Fact]
    public void Calculate_NegativeGross_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            PayrollCalculationService.Calculate(new PayrollCalculationService.Input(-1m, 6.35m, 30m, 15m)));
    }
}
