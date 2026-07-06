using Erp.Modules.Sales.Application.Features.Orders;
using FluentValidation;

namespace Erp.Modules.Sales.Application.Validators;

public class CreateSalesOrderCommandValidator : AbstractValidator<CreateSalesOrderCommand>
{
    public CreateSalesOrderCommandValidator()
    {
        RuleFor(x => x.Number).NotEmpty().MaximumLength(50);
        RuleFor(x => x.OrderDate).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("El pedido debe tener al menos una línea.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.Quantity).GreaterThan(0);
            line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0);
        });

        When(x => !x.ClientId.HasValue, () =>
        {
            RuleFor(x => x.ClientName).NotEmpty()
                .WithMessage("Indica un cliente registrado o un nombre de cliente manual.");
        });
    }
}
