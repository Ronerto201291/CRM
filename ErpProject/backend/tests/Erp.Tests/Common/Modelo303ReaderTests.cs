using Xunit;
using Erp.Application.Common.Fiscal;

namespace Erp.Tests.Common;

public class Modelo303ReaderTests
{
    [Theory]
    [InlineData(1, 1, 4)]
    [InlineData(2, 4, 7)]
    [InlineData(3, 7, 10)]
    [InlineData(4, 10, 1)]
    public void QuarterRange_CoversThreeMonths(int quarter, int startMonth, int endMonth)
    {
        var (from, to) = FiscalQuarterHelper.QuarterRange(2026, quarter);
        Assert.Equal(startMonth, from.Month);
        Assert.Equal(endMonth, to.Month);
        Assert.Equal(3, (to - from).TotalDays / 30, 0);
    }

    [Theory]
    [InlineData(21, "01", "02")]
    [InlineData(10, "04", "05")]
    [InlineData(5.2, "31", "32")]
    public void Casillas_MapOfficialRates(decimal rate, string casBase, string casCuota)
    {
        var (b, c) = rate is 5.2m or 1.4m or 0.5m
            ? FiscalQuarterHelper.SurchargeRateToCasillas(rate)
            : FiscalQuarterHelper.RateToCasillas(rate);
        Assert.Equal(casBase, b);
        Assert.Equal(casCuota, c);
    }
}
