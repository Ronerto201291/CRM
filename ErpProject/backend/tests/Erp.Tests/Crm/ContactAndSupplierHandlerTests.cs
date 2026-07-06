using Erp.Application.Common.Interfaces;
using Erp.Application.DTOs;
using Erp.Modules.Crm.Application.Features.Crm.Commands;
using Erp.Modules.Crm.Application.Features.Crm.Handlers;
using Erp.Modules.Crm.Application.Features.Crm.Queries;
using Erp.Modules.Crm.Domain.Entities;
using Erp.Modules.Crm.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Crm;

public class ContactHandlerTests
{
    [Fact]
    public async Task CreateContact_PersistsContact()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        var handler = new CreateContactHandler(ctx, tenant, new FakePublisher());

        var result = await handler.Handle(new CreateContactCommand
        {
            Name = "Ana García",
            Email = "ana@test.com",
            Phone = "600111222",
            Position = "Directora",
        }, CancellationToken.None);

        Assert.Equal("Ana García", result.Name);
        Assert.Single(await ctx.Contacts.ToListAsync());
    }

    [Fact]
    public async Task GetContacts_ReturnsPaginatedResults()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.Contacts.Add(new Contact
        {
            Id = Guid.NewGuid(), CompanyId = companyId,
            Name = "Contacto 1", Email = "c1@test.com",
        });
        await ctx.SaveChangesAsync();

        var handler = new GetContactsHandler(ctx);
        var result = await handler.Handle(new GetContactsQuery { Page = 1, PageSize = 10 }, CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task GetContacts_FiltersBySearch()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.Contacts.AddRange(
            new Contact { Id = Guid.NewGuid(), CompanyId = companyId, Name = "Pedro", Email = "p@test.com" },
            new Contact { Id = Guid.NewGuid(), CompanyId = companyId, Name = "Laura", Email = "laura@test.com" });
        await ctx.SaveChangesAsync();

        var handler = new GetContactsHandler(ctx);
        var result = await handler.Handle(new GetContactsQuery { Search = "laura" }, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("Laura", result.Items[0].Name);
    }

    [Fact]
    public async Task GetContactById_ReturnsDto()
    {
        var contactId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.Contacts.Add(new Contact
        {
            Id = contactId, CompanyId = companyId,
            Name = "Contacto", Email = "c@test.com",
        });
        await ctx.SaveChangesAsync();

        var handler = new GetContactByIdHandler(ctx);
        var dto = await handler.Handle(new GetContactByIdQuery { Id = contactId }, CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal("Contacto", dto!.Name);
    }

    [Fact]
    public async Task UpdateContact_UpdatesFields()
    {
        var contactId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.Contacts.Add(new Contact
        {
            Id = contactId, CompanyId = companyId,
            Name = "Old", Email = "old@test.com",
        });
        await ctx.SaveChangesAsync();

        var handler = new UpdateContactHandler(ctx);
        var ok = await handler.Handle(new UpdateContactCommand
        {
            Id = contactId,
            Name = "Updated",
            Email = "new@test.com",
        }, CancellationToken.None);

        Assert.True(ok);
        var updated = await ctx.Contacts.FindAsync(contactId);
        Assert.Equal("Updated", updated!.Name);
    }

    [Fact]
    public async Task DeleteContact_RemovesContact()
    {
        var contactId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.Contacts.Add(new Contact
        {
            Id = contactId, CompanyId = companyId,
            Name = "Borrar", Email = "del@test.com",
        });
        await ctx.SaveChangesAsync();

        var handler = new DeleteContactHandler(ctx);
        var ok = await handler.Handle(new DeleteContactCommand { Id = contactId }, CancellationToken.None);

        Assert.True(ok);
        Assert.Equal(0, await ctx.Contacts.CountAsync());
    }

    [Fact]
    public async Task AnonymizeContact_PseudoanonymizesPersonalData()
    {
        var contactId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.Contacts.Add(new Contact
        {
            Id = contactId, CompanyId = companyId,
            Name = "Persona Real", Email = "real@test.com", Phone = "600000000",
        });
        await ctx.SaveChangesAsync();

        var ok = await new AnonymizeContactHandler(ctx).Handle(new AnonymizeContactCommand { Id = contactId }, CancellationToken.None);

        Assert.True(ok);
        var contact = await ctx.Contacts.SingleAsync();
        Assert.True(contact.IsAnonymized);
        Assert.Equal("CONTACTO AN\u00D3NIMO", contact.Name);
        Assert.Empty(contact.Email);
    }

    private static CrmDbContext CreateContext(FakeTenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"crm-contact-{Guid.NewGuid()}")
            .Options;
        return new CrmDbContext(options, tenant);
    }
}

public class SupplierQueryHandlerTests
{
    [Fact]
    public async Task GetSuppliers_ReturnsPaginatedList()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.Suppliers.Add(new Supplier
        {
            Id = Guid.NewGuid(), CompanyId = companyId,
            Name = "Proveedor SL", TaxId = "B12345674",
        });
        await ctx.SaveChangesAsync();

        var handler = new GetSuppliersHandler(ctx, new FakePortalUrlProvider());
        var result = await handler.Handle(new GetSuppliersQuery(), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Proveedor SL", result.Items[0].Name);
    }

    [Fact]
    public async Task GetSupplierById_ReturnsDetail()
    {
        var supplierId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.Suppliers.Add(new Supplier
        {
            Id = supplierId, CompanyId = companyId,
            Name = "Acme Proveedor", TaxId = "B12345674",
        });
        await ctx.SaveChangesAsync();

        var handler = new GetSupplierByIdHandler(ctx, new FakePortalUrlProvider());
        var detail = await handler.Handle(new GetSupplierByIdQuery { Id = supplierId }, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal("Acme Proveedor", detail!.Name);
    }

    [Fact]
    public async Task CreateSupplier_PersistsSupplier()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        var handler = new CreateSupplierHandler(ctx, tenant, new FakePublisher(), new FakePortalUrlProvider());

        var result = await handler.Handle(new CreateSupplierCommand
        {
            Name = "Nuevo Proveedor",
            TaxId = "B98765432",
            Email = "prov@test.com",
        }, CancellationToken.None);

        Assert.Equal("Nuevo Proveedor", result.Name);
        Assert.Single(await ctx.Suppliers.ToListAsync());
    }

    [Fact]
    public async Task AnonymizeSupplier_PseudoanonymizesWhileKeepingTaxId()
    {
        var supplierId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.Suppliers.Add(new Supplier
        {
            Id = supplierId, CompanyId = companyId,
            Name = "Proveedor Real", TaxId = "B12345674",
            Email = "prov@test.com", Phone = "600111222",
        });
        await ctx.SaveChangesAsync();

        var ok = await new AnonymizeSupplierHandler(ctx).Handle(new AnonymizeSupplierCommand { Id = supplierId }, CancellationToken.None);

        Assert.True(ok);
        var supplier = await ctx.Suppliers.SingleAsync();
        Assert.True(supplier.IsAnonymized);
        Assert.Equal("PROVEEDOR AN\u00D3NIMO", supplier.Name);
        Assert.Equal("B12345674", supplier.TaxId);
    }

    private static CrmDbContext CreateContext(FakeTenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"crm-supplier-{Guid.NewGuid()}")
            .Options;
        return new CrmDbContext(options, tenant);
    }
}
