using Erp.Modules.Crm.Application.Features.Notes;
using Erp.Modules.Crm.Domain.Entities;
using Erp.Modules.Crm.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Crm;

public class NoteHandlerTests
{
    private static CrmDbContext CreateContext(FakeTenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"notes-{Guid.NewGuid()}")
            .Options;
        return new CrmDbContext(options, tenant);
    }

    [Fact]
    public async Task CreateNote_PersistsForClientEntity()
    {
        var companyId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.Clients.Add(new Client
        {
            Id = clientId, CompanyId = companyId,
            Name = "Cliente", TaxId = "B12345674", CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new CreateNoteHandler(ctx, tenant);
        var note = await handler.Handle(
            new CreateNoteCommand("Client", clientId, "Seguimiento", "Llamada realizada"), CancellationToken.None);

        Assert.Equal("Seguimiento", note.Title);
        Assert.Single(await ctx.Notes.ToListAsync());
    }

    [Fact]
    public async Task GetNotes_ReturnsOrderedByCreatedAt()
    {
        var companyId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.Notes.AddRange(
            new CrmNote
            {
                Id = Guid.NewGuid(), CompanyId = companyId,
                EntityType = "Lead", EntityId = entityId,
                Content = "Nota antigua", CreatedAt = DateTime.UtcNow.AddDays(-2),
            },
            new CrmNote
            {
                Id = Guid.NewGuid(), CompanyId = companyId,
                EntityType = "Lead", EntityId = entityId,
                Content = "Nota reciente", CreatedAt = DateTime.UtcNow,
            });
        await ctx.SaveChangesAsync();

        var handler = new GetNotesHandler(ctx);
        var notes = await handler.Handle(new GetNotesQuery("Lead", entityId), CancellationToken.None);

        Assert.Equal(2, notes.Count);
        Assert.Equal("Nota reciente", notes[0].Content);
    }

    [Fact]
    public async Task UpdateNote_ChangesContent()
    {
        var companyId = Guid.NewGuid();
        var noteId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.Notes.Add(new CrmNote
        {
            Id = noteId, CompanyId = companyId,
            EntityType = "Client", EntityId = Guid.NewGuid(),
            Content = "Original", CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new UpdateNoteHandler(ctx);
        var updated = await handler.Handle(new UpdateNoteCommand(noteId, "Título", "Actualizado"), CancellationToken.None);

        Assert.Equal("Actualizado", updated.Content);
    }

    [Fact]
    public async Task DeleteNote_RemovesFromDatabase()
    {
        var companyId = Guid.NewGuid();
        var noteId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.Notes.Add(new CrmNote
        {
            Id = noteId, CompanyId = companyId,
            EntityType = "Client", EntityId = Guid.NewGuid(),
            Content = "Borrar", CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new DeleteNoteHandler(ctx);
        await handler.Handle(new DeleteNoteCommand(noteId), CancellationToken.None);

        Assert.Empty(await ctx.Notes.ToListAsync());
    }
}
