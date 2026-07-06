using Erp.Modules.Crm.Application.Features.Crm.Commands;
using Erp.Modules.Crm.Application.Features.Crm.Validators;
using FluentValidation.TestHelper;
using Xunit;

namespace Erp.Tests.Common;

public class CreateClientValidatorTests
{
    private readonly CreateClientValidator _validator = new();

    [Fact]
    public void ValidClient_Passes()
    {
        var result = _validator.TestValidate(new CreateClientCommand
        {
            Name = "Cliente SL",
            Email = "cliente@test.com",
            TaxId = "B12345674",
        });
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void InvalidTaxId_Fails()
    {
        var result = _validator.TestValidate(new CreateClientCommand
        {
            Name = "Cliente",
            Email = "cliente@test.com",
            TaxId = "XXXX",
        });
        result.ShouldHaveValidationErrorFor(x => x.TaxId);
    }

    [Fact]
    public void EmptyEmail_Fails()
    {
        var result = _validator.TestValidate(new CreateClientCommand
        {
            Name = "Cliente",
            Email = "",
            TaxId = "12345678Z",
        });
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }
}
