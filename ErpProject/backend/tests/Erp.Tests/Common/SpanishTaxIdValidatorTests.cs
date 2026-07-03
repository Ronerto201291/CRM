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

    // CIFs reales conocidos (no generados por fuerza bruta contra el propio
    // validador — eso ocultaba la inversión letra/dígito descrita en
    // ADR-0013/ADR-0018 ítem 0f, porque el "CIF válido" se obtenía forzando
    // hasta que el validador ya roto lo aceptara).
    [Theory]
    [InlineData("A39000013")] // Banco Santander — A lleva siempre dígito de control
    [InlineData("A28015865")] // Telefónica — A lleva siempre dígito de control
    public void IsValid_AcceptsKnownRealCif(string taxId)
    {
        Assert.True(SpanishTaxIdValidator.IsValid(taxId));
    }

    [Theory]
    [InlineData("A39000010")] // dígito de control alterado
    [InlineData("A39000019")]
    public void IsValid_RejectsCifWithWrongControlDigit(string taxId)
    {
        Assert.False(SpanishTaxIdValidator.IsValid(taxId));
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
