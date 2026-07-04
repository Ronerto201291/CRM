using Erp.Modules.Crm.Application.Features.Services.Commands;
using FluentValidation;

namespace Erp.Modules.Crm.Application.Features.Services.Validators;

public class CreateServiceCatalogItemValidator : AbstractValidator<CreateServiceCatalogItemCommand>
{
    public CreateServiceCatalogItemValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DefaultPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DefaultTaxRate).InclusiveBetween(0, 100);
        RuleFor(x => x.DefaultPeriodicity).Must(ServicePeriodicity.IsValid)
            .WithMessage("Periodicidad no válida: debe ser Monthly, Quarterly o Yearly.");
    }
}

public class UpdateServiceCatalogItemValidator : AbstractValidator<UpdateServiceCatalogItemCommand>
{
    public UpdateServiceCatalogItemValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DefaultPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DefaultTaxRate).InclusiveBetween(0, 100);
        RuleFor(x => x.DefaultPeriodicity).Must(ServicePeriodicity.IsValid)
            .WithMessage("Periodicidad no válida: debe ser Monthly, Quarterly o Yearly.");
    }
}
