using Erp.Application.Common.Validation;
using FluentValidation;

namespace Erp.Application.Features.Auth.Validators;

public class RegisterCompanyCommandValidator : AbstractValidator<Commands.RegisterCompanyCommand>
{
    public RegisterCompanyCommandValidator()
    {
        RuleFor(x => x.CompanyName)
            .NotEmpty().WithMessage("El nombre de la empresa es requerido.")
            .MaximumLength(200).WithMessage("El nombre no puede superar 200 caracteres.");

        RuleFor(x => x.CompanyTaxId)
            .NotEmpty().WithMessage("El CIF/NIF es requerido.")
            .MaximumLength(20).WithMessage("El CIF/NIF no puede superar 20 caracteres.")
            .Must(SpanishTaxIdValidator.IsValid).WithMessage("NIF/CIF/NIE no válido.");

        RuleFor(x => x.CompanyAddress)
            .NotEmpty().WithMessage("La dirección es requerida.")
            .MaximumLength(500).WithMessage("La dirección no puede superar 500 caracteres.");

        RuleFor(x => x.AdminEmail)
            .NotEmpty().WithMessage("El email del administrador es requerido.")
            .EmailAddress().WithMessage("El email debe tener un formato válido.")
            .MaximumLength(200);

        RuleFor(x => x.AdminPassword)
            .NotEmpty().WithMessage("La contraseña es requerida.")
            .MinimumLength(8).WithMessage("La contraseña debe tener al menos 8 caracteres.")
            .Matches(@"[A-Z]").WithMessage("Debe contener al menos una mayúscula.")
            .Matches(@"[0-9]").WithMessage("Debe contener al menos un número.");

        RuleFor(x => x.AdminFirstName)
            .NotEmpty().WithMessage("El nombre es requerido.")
            .MaximumLength(100);

        RuleFor(x => x.AdminLastName)
            .NotEmpty().WithMessage("Los apellidos son requeridos.")
            .MaximumLength(100);
    }
}
