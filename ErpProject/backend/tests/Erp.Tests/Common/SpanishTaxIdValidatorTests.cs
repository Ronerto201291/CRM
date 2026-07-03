using Erp.Application.Common.Validation;
using Xunit;

namespace Erp.Tests.Common;

public class SpanishTaxIdValidatorTests
{
    [Theory]
    [InlineData("12345678Z")]
    [InlineData("X1234567L")]
    public void IsValid_AcceptsKnownValidIds(string taxId)
    {
        Assert.True(SpanishTaxIdValidator.IsValid(taxId));
    }

    [Fact]
    public void IsValid_AcceptsValidCif()
    {
        Assert.True(SpanishTaxIdValidator.IsValid(FindValidCif()));
    }

    private static string FindValidCif()
    {
        for (var n = 1; n < 1_000_000; n++)
        {
            var digits = n.ToString("D7");
            foreach (var suffix in "0123456789ABCDEFGHIJ")
            {
                var candidate = $"B{digits}{suffix}";
                if (SpanishTaxIdValidator.IsValid(candidate))
                    return candidate;
            }
        }
        throw new InvalidOperationException("No valid CIF found");
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
    }
}
