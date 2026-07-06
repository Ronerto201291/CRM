using Erp.Application.Common.Validation;
using Xunit;

namespace Erp.Tests.Common;

public class SpanishSocialSecurityNumberValidatorTests
{
    [Theory]
    [InlineData("281234567840")]
    [InlineData("28 12345678 40")]
    [InlineData("28/12345678/40")]
    public void IsValidNaf_accepts_known_valid_number(string naf)
    {
        Assert.True(SpanishSocialSecurityNumberValidator.IsValidNaf(naf));
    }

    [Fact]
    public void ComputeControlDigits_matches_letranif_example()
    {
        var dc = SpanishSocialSecurityNumberValidator.ComputeControlDigits(28, 12_345_678);
        Assert.Equal(40, dc);
    }

    [Fact]
    public void CompleteCcc_appends_control_digits()
    {
        var ccc = SpanishSocialSecurityNumberValidator.CompleteCcc("281234567");
        Assert.Equal(11, ccc.Length);
        Assert.True(SpanishSocialSecurityNumberValidator.IsValidCcc(ccc));
        Assert.EndsWith("42", ccc);
    }

    [Fact]
    public void IsValidNaf_rejects_wrong_control()
    {
        Assert.False(SpanishSocialSecurityNumberValidator.IsValidNaf("281234567899"));
    }
}
