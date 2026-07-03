using Erp.Application.Common.Validation;
using Xunit;

namespace Erp.Tests.Common;

public class SpanishTaxIdValidatorTests
{
    [Theory]
    [InlineData("12345678Z")]
    [InlineData("X1234567L")]
    public void IsValid_AcceptsKnownValidNifAndNie(string taxId)
    {
        Assert.True(SpanishTaxIdValidator.IsValid(taxId));
    }

    [Theory]
    [InlineData("A39000013")] // Banco Santander S.A.
    [InlineData("B12345674")] // CIF de referencia habitual en tests
    public void IsValid_AcceptsKnownValidCif(string taxId)
    {
        Assert.True(SpanishTaxIdValidator.IsValid(taxId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234567")]
    [InlineData("1234567890")]
    [InlineData("12345678A")]
    [InlineData("INVALID")]
    public void IsValid_RejectsInvalidIds(string? taxId)
    {
        Assert.False(SpanishTaxIdValidator.IsValid(taxId));
    }

    [Fact]
    public void IsValid_NormalizesSpacesAndDashes()
    {
        Assert.True(SpanishTaxIdValidator.IsValid("12345678-Z"));
        Assert.True(SpanishTaxIdValidator.IsValid("1234 5678 Z"));
        Assert.True(SpanishTaxIdValidator.IsValid("A-39000013"));
    }
}
