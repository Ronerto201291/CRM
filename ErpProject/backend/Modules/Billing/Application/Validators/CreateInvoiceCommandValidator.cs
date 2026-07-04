using Erp.Application.Common.Validation;
using Erp.Modules.Billing.Application.Features.Billing.Commands;
using FluentValidation;

namespace Erp.Modules.Billing.Application.Validators;

public class CreateInvoiceCommandValidator : AbstractValidator<CreateInvoiceCommand>
{
    public CreateInvoiceCommandValidator()
    {
        RuleFor(x => x.Series).NotEmpty().MaximumLength(10);
        RuleFor(x => x.DueDate).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("La factura debe tener al menos una línea.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.Description).NotEmpty();
            line.RuleFor(l => l.Quantity).GreaterThan(0);
            line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0);
        });

        When(x => x.ClientType == "Registered", () =>
        {
            RuleFor(x => x.ClientId).NotNull().WithMessage("Indica el cliente registrado.");
        });

        When(x => x.ClientType == "Manual", () =>
        {
            RuleFor(x => x.ClientName).NotEmpty().WithMessage("Indica el nombre del cliente.");
        });

        When(x => !string.IsNullOrWhiteSpace(x.ClientTaxId), () =>
        {
            RuleFor(x => x.ClientTaxId!)
                .Must(SpanishTaxIdValidator.IsValid)
                .WithMessage("NIF/CIF/NIE del cliente no válido.");
        });
    }
}
