using Erp.Application.Features.Auth.Commands;
using Erp.Application.Features.Auth.Validators;
using FluentValidation.TestHelper;
using Xunit;

namespace Erp.Tests.Common;

public class RegisterCompanyCommandValidatorTests
{
    private readonly RegisterCompanyCommandValidator _validator = new();

    private static RegisterCompanyCommand Valid() => new()
    {
        CompanyName = "Startup SL",
        CompanyTaxId = "B12345674",
        CompanyAddress = "Calle Test 1",
        AdminEmail = "admin@test.com",
        AdminPassword = "SecurePass1!",
        AdminFirstName = "Ana",
        AdminLastName = "García",
    };

    [Fact]
    public void ValidCommand_Passes()
    {
        var result = _validator.TestValidate(Valid());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void InvalidTaxId_Fails()
    {
        var cmd = Valid();
        cmd.CompanyTaxId = "INVALID";
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.CompanyTaxId);
    }

    [Fact]
    public void WeakPassword_Fails()
    {
        var cmd = Valid();
        cmd.AdminPassword = "weak";
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.AdminPassword);
    }

    [Fact]
    public void EmptyCompanyName_Fails()
    {
        var cmd = Valid();
        cmd.CompanyName = "";
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.CompanyName);
    }
}
