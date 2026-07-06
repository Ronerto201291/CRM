using Erp.Modules.Treasury.Application.Features.Treasury.Commands;
using FluentValidation;

namespace Erp.Modules.Treasury.Application.Validators;

public class CreateBankAccountCommandValidator : AbstractValidator<CreateBankAccountCommand>
{
    public CreateBankAccountCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Iban).NotEmpty().MinimumLength(15).MaximumLength(34);
        RuleFor(x => x.BankName).NotEmpty().MaximumLength(200);
    }
}
