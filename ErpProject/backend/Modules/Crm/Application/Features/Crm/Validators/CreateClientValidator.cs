using Erp.Modules.Crm.Application.Features.Crm.Commands;
using FluentValidation;

namespace Erp.Modules.Crm.Application.Features.Crm.Validators;

public class CreateClientValidator : AbstractValidator<CreateClientCommand>
{
    public CreateClientValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.TaxId).NotEmpty().MaximumLength(50);
    }
}
