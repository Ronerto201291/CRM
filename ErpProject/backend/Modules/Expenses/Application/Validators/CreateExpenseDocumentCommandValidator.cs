using Erp.Modules.Expenses.Application.Features.Expenses.Commands;
using FluentValidation;

namespace Erp.Modules.Expenses.Application.Validators;

public class CreateExpenseDocumentCommandValidator : AbstractValidator<CreateExpenseDocumentCommand>
{
    public CreateExpenseDocumentCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.SupplierName) || !string.IsNullOrWhiteSpace(x.InvoiceNumber))
            .WithMessage("Indica proveedor o número de factura.");

        When(x => x.Total.HasValue, () =>
        {
            RuleFor(x => x.Total!.Value).GreaterThanOrEqualTo(0);
        });
    }
}
