using Erp.Application.Common.Behaviors;
using Erp.Modules.Billing.Application.Features.Billing.Commands;
using Erp.Modules.Billing.Application.Validators;
using Erp.Modules.Expenses.Application.Features.Expenses.Commands;
using Erp.Modules.Expenses.Application.Validators;
using Erp.Modules.Sales.Application.Features.Orders;
using Erp.Modules.Sales.Application.Validators;
using Erp.Modules.Treasury.Application.Features.Treasury.Commands;
using Erp.Modules.Treasury.Application.Validators;
using FluentValidation;
using FluentValidation.TestHelper;
using MediatR;
using Xunit;

namespace Erp.Tests.Common;

public class CreateInvoiceCommandValidatorTests
{
    private readonly CreateInvoiceCommandValidator _validator = new();

    [Fact]
    public void ValidManualInvoice_Passes()
    {
        var result = _validator.TestValidate(new CreateInvoiceCommand
        {
            ClientType = "Manual",
            ClientName = "Cliente SL",
            ClientTaxId = "B12345674",
            DueDate = DateTime.UtcNow.AddDays(30),
            Lines = [new CreateInvoiceLineDto { Description = "Servicio", Quantity = 1, UnitPrice = 100, TaxRate = 21 }],
        });
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyLines_Fails()
    {
        var result = _validator.TestValidate(new CreateInvoiceCommand
        {
            ClientType = "Manual",
            ClientName = "Cliente",
            DueDate = DateTime.UtcNow.AddDays(30),
            Lines = [],
        });
        result.ShouldHaveValidationErrorFor(x => x.Lines);
    }

    [Fact]
    public void RegisteredWithoutClientId_Fails()
    {
        var result = _validator.TestValidate(new CreateInvoiceCommand
        {
            ClientType = "Registered",
            DueDate = DateTime.UtcNow.AddDays(30),
            Lines = [new CreateInvoiceLineDto { Description = "Línea", Quantity = 1, UnitPrice = 10, TaxRate = 21 }],
        });
        result.ShouldHaveValidationErrorFor(x => x.ClientId);
    }

    [Fact]
    public void InvalidTaxId_Fails()
    {
        var result = _validator.TestValidate(new CreateInvoiceCommand
        {
            ClientType = "Manual",
            ClientName = "Cliente",
            ClientTaxId = "INVALID",
            DueDate = DateTime.UtcNow.AddDays(30),
            Lines = [new CreateInvoiceLineDto { Description = "Línea", Quantity = 1, UnitPrice = 10, TaxRate = 21 }],
        });
        result.ShouldHaveValidationErrorFor(x => x.ClientTaxId);
    }
}

public class CreateSalesOrderCommandValidatorTests
{
    private readonly CreateSalesOrderCommandValidator _validator = new();

    [Fact]
    public void ValidOrder_Passes()
    {
        var result = _validator.TestValidate(new CreateSalesOrderCommand(
            "PED-001",
            DateTime.UtcNow,
            Guid.NewGuid(),
            "Cliente",
            [new SalesOrderLineDto(null, 2m, 50m)]));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyLines_Fails()
    {
        var result = _validator.TestValidate(new CreateSalesOrderCommand(
            "PED-002", DateTime.UtcNow, null, "Cliente manual", []));
        result.ShouldHaveValidationErrorFor(x => x.Lines);
    }

    [Fact]
    public void ManualWithoutClientName_Fails()
    {
        var result = _validator.TestValidate(new CreateSalesOrderCommand(
            "PED-003", DateTime.UtcNow, null, "", [new SalesOrderLineDto(null, 1m, 10m)]));
        result.ShouldHaveValidationErrorFor(x => x.ClientName);
    }
}

public class CreateExpenseDocumentCommandValidatorTests
{
    private readonly CreateExpenseDocumentCommandValidator _validator = new();

    [Fact]
    public void ValidExpense_Passes()
    {
        var result = _validator.TestValidate(new CreateExpenseDocumentCommand
        {
            SupplierName = "Proveedor SL",
            Total = 121m,
        });
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void MissingSupplierAndInvoice_Fails()
    {
        var result = _validator.TestValidate(new CreateExpenseDocumentCommand());
        result.ShouldHaveValidationErrorFor(x => x);
    }
}

public class CreateBankAccountCommandValidatorTests
{
    private readonly CreateBankAccountCommandValidator _validator = new();

    [Fact]
    public void ValidAccount_Passes()
    {
        var result = _validator.TestValidate(new CreateBankAccountCommand(
            "Cuenta principal", "ES9121000418450200051332", "CAIXESBBXXX", "CaixaBank", "572"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyIban_Fails()
    {
        var result = _validator.TestValidate(new CreateBankAccountCommand(
            "Cuenta", "", null, "Banco", null));
        result.ShouldHaveValidationErrorFor(x => x.Iban);
    }

    [Fact]
    public void EmptyName_Fails()
    {
        var result = _validator.TestValidate(new CreateBankAccountCommand(
            "", "ES9121000418450200051332", null, "Banco", null));
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }
}

public class ValidationBehaviorModuleValidatorTests
{
    [Fact]
    public async Task CreateInvoice_WhenInvalid_ThrowsValidationException()
    {
        var behavior = new ValidationBehavior<CreateInvoiceCommand, Erp.Application.DTOs.InvoiceDto>(
            [new CreateInvoiceCommandValidator()]);
        var called = false;

        await Assert.ThrowsAsync<ValidationException>(() =>
            behavior.Handle(
                new CreateInvoiceCommand { ClientType = "Manual", DueDate = DateTime.UtcNow, Lines = [] },
                _ =>
                {
                    called = true;
                    return Task.FromResult(new Erp.Application.DTOs.InvoiceDto());
                },
                CancellationToken.None));

        Assert.False(called);
    }

    [Fact]
    public async Task CreateExpense_WhenValid_CallsNext()
    {
        var behavior = new ValidationBehavior<CreateExpenseDocumentCommand, Guid>(
            [new CreateExpenseDocumentCommandValidator()]);

        var id = await behavior.Handle(
            new CreateExpenseDocumentCommand { SupplierName = "Proveedor" },
            _ => Task.FromResult(Guid.NewGuid()),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, id);
    }
}
