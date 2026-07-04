using Erp.Modules.Crm.Application.Features.Services.Commands;
using Erp.Modules.Crm.Application.Features.Services.Validators;
using Xunit;

namespace Erp.Tests.Crm;

public class ServiceCatalogValidatorsTests
{
    [Fact]
    public void CreateServiceCatalogItemValidator_WithValidData_Passes()
    {
        var validator = new CreateServiceCatalogItemValidator();
        var result = validator.Validate(new CreateServiceCatalogItemCommand
        {
            Name = "Mantenimiento anual",
            DefaultPrice = 250m,
            DefaultTaxRate = 21m,
            DefaultPeriodicity = "Yearly",
        });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", 100, 21, "Monthly")]      // nombre vacío
    [InlineData("X", -1, 21, "Monthly")]      // precio negativo
    [InlineData("X", 100, 150, "Monthly")]    // IVA fuera de rango
    [InlineData("X", 100, 21, "Weekly")]      // periodicidad no válida
    public void CreateServiceCatalogItemValidator_WithInvalidData_Fails(string name, decimal price, decimal taxRate, string periodicity)
    {
        var validator = new CreateServiceCatalogItemValidator();
        var result = validator.Validate(new CreateServiceCatalogItemCommand
        {
            Name = name,
            DefaultPrice = price,
            DefaultTaxRate = taxRate,
            DefaultPeriodicity = periodicity,
        });

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("", 100, 21, "Monthly")]
    [InlineData("X", -1, 21, "Monthly")]
    [InlineData("X", 100, 21, "Weekly")]
    public void UpdateServiceCatalogItemValidator_WithInvalidData_Fails(string name, decimal price, decimal taxRate, string periodicity)
    {
        var validator = new UpdateServiceCatalogItemValidator();
        var result = validator.Validate(new UpdateServiceCatalogItemCommand
        {
            Id = Guid.NewGuid(),
            Name = name,
            DefaultPrice = price,
            DefaultTaxRate = taxRate,
            DefaultPeriodicity = periodicity,
        });

        Assert.False(result.IsValid);
    }
}
