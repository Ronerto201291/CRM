using Erp.Application.Common.Validation;
using Erp.Modules.Crm.Application.Features.Crm.Commands;
using FluentValidation;

namespace Erp.Modules.Crm.Application.Features.Crm.Validators;

public class UpdateClientValidator : AbstractValidator<UpdateClientCommand>
{
    public UpdateClientValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.TaxId).NotEmpty().MaximumLength(50)
            .Must(SpanishTaxIdValidator.IsValid).WithMessage("NIF/CIF/NIE no válido");
    }
}
