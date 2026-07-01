using FluentValidation;

namespace Erp.Application.Features.Auth.Validators;

public class LoginCommandValidator : AbstractValidator<Commands.LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El email no puede estar vacío.")
            .EmailAddress().WithMessage("El email debe tener un formato válido.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es requerida.");
    }
}
