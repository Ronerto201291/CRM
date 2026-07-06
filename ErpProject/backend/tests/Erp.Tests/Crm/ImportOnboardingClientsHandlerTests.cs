using Erp.Modules.Crm.Application.Features.Crm.Commands;
using Erp.Modules.Crm.Application.Features.Onboarding;
using Erp.Modules.Crm.Domain.Entities;
using Erp.Modules.Crm.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Crm;

/// <summary>
/// El handler central de onboarding (el que de verdad persiste clientes) no tenía ningún
/// test — solo el parser (OnboardingImportParserTests) y el handler de plantillas contables
/// de Accounting estaban cubiertos (contra-auditoría jul 2026).
/// </summary>
public class ImportOnboardingClientsHandlerTests
{
    private const string ValidCif = "A39000013"; // Banco Santander, CIF real conocido

    [Fact]
    public async Task Handle_ValidCsvRows_CreatesClientsViaMediator()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        var mediator = new FakeMediator(_ => new Erp.Application.DTOs.ClientDto());

        var handler = new ImportOnboardingClientsHandler(mediator, ctx, tenant);
        var csv = $"Nombre,CIF,Email\nCliente Uno,{ValidCif},uno@test.com";

        var result = await handler.Handle(new ImportOnboardingClientsCommand(CsvContent: csv), CancellationToken.None);

        Assert.Equal(1, result.Imported);
        Assert.Equal(0, result.Skipped);
        Assert.Empty(result.Errors);
        var sent = Assert.Single(mediator.SentRequests);
        var command = Assert.IsType<CreateClientCommand>(sent);
        Assert.Equal("Cliente Uno", command.Name);
        Assert.Equal(ValidCif, command.TaxId);
    }

    [Fact]
    public async Task Handle_DuplicateTaxIdAlreadyInCrm_SkipsWithoutCallingMediator()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.Clients.Add(new Client
        {
            Id = Guid.NewGuid(), CompanyId = companyId, Name = "Ya existe",
            TaxId = ValidCif, Email = "existe@test.com",
        });
        await ctx.SaveChangesAsync();

        var mediator = new FakeMediator();
        var handler = new ImportOnboardingClientsHandler(mediator, ctx, tenant);
        var csv = $"Nombre,CIF,Email\nCliente Duplicado,{ValidCif},dup@test.com";

        var result = await handler.Handle(new ImportOnboardingClientsCommand(CsvContent: csv), CancellationToken.None);

        Assert.Equal(0, result.Imported);
        Assert.Equal(1, result.Skipped);
        Assert.Empty(mediator.SentRequests);
    }

    [Fact]
    public async Task Handle_InvalidCif_SkipsRowWithLineError()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        var mediator = new FakeMediator();
        var handler = new ImportOnboardingClientsHandler(mediator, ctx, tenant);
        var csv = "Nombre,CIF,Email\nCliente Malo,XXXXXXXX,malo@test.com";

        var result = await handler.Handle(new ImportOnboardingClientsCommand(CsvContent: csv), CancellationToken.None);

        Assert.Equal(0, result.Imported);
        Assert.Equal(1, result.Skipped);
        Assert.Contains(result.Errors, e => e.Contains("Línea 2") && e.Contains("CIF/NIF inválido"));
    }

    [Fact]
    public async Task Handle_NoContent_ReturnsNoRowsFoundError()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");

        await using var ctx = CreateContext(tenant);
        var handler = new ImportOnboardingClientsHandler(new FakeMediator(), ctx, tenant);

        var result = await handler.Handle(new ImportOnboardingClientsCommand(), CancellationToken.None);

        Assert.Equal(0, result.Imported);
        Assert.Single(result.Errors);
    }

    private static CrmDbContext CreateContext(FakeTenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"onboarding-import-{Guid.NewGuid()}")
            .Options;
        return new CrmDbContext(options, tenant);
    }
}
