using Xunit;

namespace Erp.Tests.Common;

public class RecargoHandlerTests
{
    [Theory]
    [InlineData(1000, 5.2, 52)]
    [InlineData(500, 1.4, 7)]
    [InlineData(200, 0.5, 1)]
    public void RechargeAmount_CalculatesCorrectly(decimal baseAmount, decimal rate, decimal expected)
    {
        var amount = Math.Round(baseAmount * (rate / 100m), 2);
        Assert.Equal(expected, amount);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.4)]
    [InlineData(5.2)]
    public void AllowedRates_AcceptOfficialValues(decimal rate)
    {
        var allowed = new HashSet<decimal> { 0.5m, 1.4m, 5.2m };
        Assert.Contains(rate, allowed);
    }

    [Theory]
    [InlineData(3.0)]
    [InlineData(10.0)]
    public void AllowedRates_RejectUnknownValues(decimal rate)
    {
        var allowed = new HashSet<decimal> { 0.5m, 1.4m, 5.2m };
        Assert.DoesNotContain(rate, allowed);
    }
}
