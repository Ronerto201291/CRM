using Erp.Application.Common.Validation;
using Erp.Modules.Crm.Application.Features.Crm.Commands;
using FluentValidation;

namespace Erp.Modules.Crm.Application.Features.Crm.Validators;

public class CreateSupplierValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TaxId).NotEmpty().MaximumLength(50)
            .Must(SpanishTaxIdValidator.IsValid).WithMessage("NIF/CIF/NIE no válido");
    }
}
