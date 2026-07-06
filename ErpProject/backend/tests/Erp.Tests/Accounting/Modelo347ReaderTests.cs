using Erp.Modules.Accounting.Infrastructure.Services;
using Xunit;

namespace Erp.Tests.Accounting;

public class Modelo347ReaderTests
{
    [Theory]
    [InlineData("12345678Z", true)]
    [InlineData("87654321A", true)]
    [InlineData("B12345678", false)]
    [InlineData("X1234567L", false)]
    [InlineData("", false)]
    public void EsPersonaFisica_ClassifiesNif(string nif, bool expected)
    {
        Assert.Equal(expected, Modelo347Reader.EsPersonaFisica(nif));
    }

    [Fact]
    public void Threshold347_MatchesLegalMinimum()
    {
        Assert.Equal(3005.06m, Modelo347Reader.Threshold347);
    }
}
