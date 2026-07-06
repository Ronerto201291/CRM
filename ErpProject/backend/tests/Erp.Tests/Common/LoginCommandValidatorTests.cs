using Erp.Application.Features.Auth.Commands;
using Erp.Application.Features.Auth.Validators;
using FluentValidation.TestHelper;
using Xunit;

namespace Erp.Tests.Common;

public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public void ValidLogin_Passes()
    {
        var result = _validator.TestValidate(new LoginCommand
        {
            Email = "user@test.com",
            Password = "secret",
        });
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void InvalidEmail_Fails()
    {
        var result = _validator.TestValidate(new LoginCommand
        {
            Email = "not-an-email",
            Password = "secret",
        });
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void EmptyPassword_Fails()
    {
        var result = _validator.TestValidate(new LoginCommand
        {
            Email = "user@test.com",
            Password = "",
        });
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }
}
