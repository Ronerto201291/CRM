using Erp.Modules.Payroll.Application.Features.Employees;
using Erp.Modules.Payroll.Domain.Entities;
using Erp.Modules.Payroll.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Payroll;

public class GetEmployeesHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsActiveEmployees()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<PayrollDbContext>()
            .UseInMemoryDatabase($"payroll-employees-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new PayrollDbContext(options, tenant);
        ctx.Employees.AddRange(
            new Employee
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                TaxId = "12345678A",
                FullName = "Ana Activa",
                HireDate = DateTime.UtcNow.Date,
                ContractType = "Indefinido",
                WeeklyHours = 40,
                IsActive = true,
            },
            new Employee
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                TaxId = "87654321B",
                FullName = "Baja Inactiva",
                HireDate = DateTime.UtcNow.Date,
                ContractType = "Temporal",
                WeeklyHours = 20,
                IsActive = false,
            });
        await ctx.SaveChangesAsync();

        var handler = new GetEmployeesHandler(ctx);
        var result = await handler.Handle(new GetEmployeesQuery(), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("Ana Activa", result[0].FullName);
    }
}

public class CreateEmployeeHandlerTests
{
    [Fact]
    public async Task Handle_PersistsEmployeeForTenant()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<PayrollDbContext>()
            .UseInMemoryDatabase($"payroll-create-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new PayrollDbContext(options, tenant);
        var handler = new CreateEmployeeHandler(ctx, tenant);

        var result = await handler.Handle(new CreateEmployeeCommand(
            "12345678Z",
            "Nuevo Empleado",
            "SS-123",
            new DateTime(2026, 1, 1),
            "Indefinido",
            40m), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        var stored = await ctx.Employees.SingleAsync();
        Assert.Equal(companyId, stored.CompanyId);
        Assert.Equal("Nuevo Empleado", stored.FullName);
    }
}
