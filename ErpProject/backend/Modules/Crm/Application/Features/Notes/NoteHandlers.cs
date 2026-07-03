using Erp.Application.Common.Interfaces;
using Erp.Modules.Crm.Application.Interfaces;
using Erp.Modules.Crm.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Crm.Application.Features.Notes;

public record NoteDto(
    Guid Id, string EntityType, Guid EntityId, string? Title,
    string Content, DateTime CreatedAt, DateTime? UpdatedAt);

public record GetNotesQuery(string EntityType, Guid EntityId) : IRequest<IReadOnlyList<NoteDto>>;
public record CreateNoteCommand(string EntityType, Guid EntityId, string? Title, string Content) : IRequest<NoteDto>;
public record UpdateNoteCommand(Guid Id, string? Title, string Content) : IRequest<NoteDto>;
public record DeleteNoteCommand(Guid Id) : IRequest<Unit>;

public class GetNotesHandler : IRequestHandler<GetNotesQuery, IReadOnlyList<NoteDto>>
{
    private readonly ICrmDbContext _db;

    public GetNotesHandler(ICrmDbContext db) => _db = db;

    public async Task<IReadOnlyList<NoteDto>> Handle(GetNotesQuery request, CancellationToken ct)
    {
        return await _db.Notes
            .Where(n => n.EntityType == request.EntityType && n.EntityId == request.EntityId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NoteDto(n.Id, n.EntityType, n.EntityId, n.Title, n.Content, n.CreatedAt, n.UpdatedAt))
            .ToListAsync(ct);
    }
}

public class CreateNoteHandler : IRequestHandler<CreateNoteCommand, NoteDto>
{
    private readonly ICrmDbContext _db;
    private readonly ITenantContext _tenant;

    public CreateNoteHandler(ICrmDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<NoteDto> Handle(CreateNoteCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            throw new ArgumentException("Content is required");

        if (!await NoteEntityValidator.EntityBelongsToTenant(_db, request.EntityType, request.EntityId, ct))
            throw new KeyNotFoundException("Entity not found");

        var note = new CrmNote
        {
            Id = Guid.NewGuid(),
            CompanyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved"),
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            Title = request.Title,
            Content = request.Content,
        };

        _db.Notes.Add(note);
        await _db.SaveChangesAsync(ct);
        return new NoteDto(note.Id, note.EntityType, note.EntityId, note.Title, note.Content, note.CreatedAt, note.UpdatedAt);
    }
}

public class UpdateNoteHandler : IRequestHandler<UpdateNoteCommand, NoteDto>
{
    private readonly ICrmDbContext _db;

    public UpdateNoteHandler(ICrmDbContext db) => _db = db;

    public async Task<NoteDto> Handle(UpdateNoteCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            throw new ArgumentException("Content is required");

        var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Note not found");

        note.Title = request.Title;
        note.Content = request.Content;
        note.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return new NoteDto(note.Id, note.EntityType, note.EntityId, note.Title, note.Content, note.CreatedAt, note.UpdatedAt);
    }
}

public class DeleteNoteHandler : IRequestHandler<DeleteNoteCommand, Unit>
{
    private readonly ICrmDbContext _db;

    public DeleteNoteHandler(ICrmDbContext db) => _db = db;

    public async Task<Unit> Handle(DeleteNoteCommand request, CancellationToken ct)
    {
        var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Note not found");

        _db.Notes.Remove(note);
        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

internal static class NoteEntityValidator
{
    internal static Task<bool> EntityBelongsToTenant(
        ICrmDbContext db, string entityType, Guid entityId, CancellationToken ct)
        => entityType switch
        {
            "Client" => db.Clients.AnyAsync(c => c.Id == entityId, ct),
            "Lead" => db.Leads.AnyAsync(l => l.Id == entityId, ct),
            _ => Task.FromResult(false),
        };
}
