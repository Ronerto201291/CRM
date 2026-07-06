using Erp.Application.Common.Events;
using Erp.Application.Common.Interfaces;
using Erp.Application.DTOs;
using Erp.Modules.Crm.Application.Features.Crm.Commands;
using Erp.Modules.Crm.Application.Features.Crm.Queries;
using Erp.Modules.Crm.Application.Interfaces;
using Erp.Modules.Crm.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Crm.Application.Features.Crm.Handlers;

public class GetContactsHandler : IRequestHandler<GetContactsQuery, PaginatedContactsResult>
{
    private readonly ICrmDbContext _ctx;
    public GetContactsHandler(ICrmDbContext ctx) => _ctx = ctx;

    public async Task<PaginatedContactsResult> Handle(GetContactsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 500);

        var query = _ctx.Contacts.AsQueryable();

        if (request.ClientId.HasValue)
            query = query.Where(c => c.ClientId == request.ClientId.Value);
        if (request.SupplierId.HasValue)
            query = query.Where(c => c.SupplierId == request.SupplierId.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(c => c.Name.Contains(request.Search)
                                  || c.Email.Contains(request.Search)
                                  || c.Position.Contains(request.Search));

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ContactDto
            {
                Id = c.Id, Name = c.Name, Email = c.Email, Phone = c.Phone,
                Position = c.Position, ClientId = c.ClientId, SupplierId = c.SupplierId,
                ClientName   = c.Client   != null ? c.Client.Name   : null,
                SupplierName = c.Supplier != null ? c.Supplier.Name : null,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync(ct);

        return new PaginatedContactsResult(items, totalCount, page, pageSize);
    }
}

public class GetContactByIdHandler : IRequestHandler<GetContactByIdQuery, ContactDto?>
{
    private readonly ICrmDbContext _ctx;
    public GetContactByIdHandler(ICrmDbContext ctx) => _ctx = ctx;

    public async Task<ContactDto?> Handle(GetContactByIdQuery request, CancellationToken ct)
    {
        return await _ctx.Contacts
            .Include(c => c.Client)
            .Include(c => c.Supplier)
            .Where(c => c.Id == request.Id)
            .Select(c => new ContactDto
            {
                Id = c.Id, Name = c.Name, Email = c.Email, Phone = c.Phone,
                Position = c.Position, ClientId = c.ClientId, SupplierId = c.SupplierId,
                ClientName   = c.Client   != null ? c.Client.Name   : null,
                SupplierName = c.Supplier != null ? c.Supplier.Name : null,
                CreatedAt = c.CreatedAt, UpdatedAt = c.UpdatedAt
            })
            .FirstOrDefaultAsync(ct);
    }
}

public class CreateContactHandler : IRequestHandler<CreateContactCommand, ContactDto>
{
    private readonly ICrmDbContext _ctx;
    private readonly ITenantContext _tenant;
    private readonly IPublisher _publisher;

    public CreateContactHandler(ICrmDbContext ctx, ITenantContext tenant, IPublisher publisher)
    {
        _ctx = ctx; _tenant = tenant; _publisher = publisher;
    }

    public async Task<ContactDto> Handle(CreateContactCommand request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new UnauthorizedAccessException("No tenant context.");

        var contact = new Contact
        {
            Id = Guid.NewGuid(), CompanyId = companyId,
            Name      = request.Name,
            Email     = request.Email     ?? string.Empty,
            Phone     = request.Phone     ?? string.Empty,
            Position  = request.Position  ?? string.Empty,
            ClientId  = request.ClientId,
            SupplierId = request.SupplierId,
        };

        _ctx.Contacts.Add(contact);
        _ctx.ActivityLogs.Add(new ActivityLog
        {
            Id = Guid.NewGuid(), CompanyId = companyId,
            EntityType = "Contact", EntityId = contact.Id,
            Action = "Created", Description = $"Contacto '{contact.Name}' creado"
        });

        await _ctx.SaveChangesAsync(ct);

        await _publisher.Publish(new ContactCreatedEvent
        {
            ContactId = contact.Id, CompanyId = companyId,
            Name = contact.Name, Email = contact.Email,
            ClientId = contact.ClientId, SupplierId = contact.SupplierId,
        }, ct);

        return new ContactDto
        {
            Id = contact.Id, Name = contact.Name, Email = contact.Email,
            Phone = contact.Phone, Position = contact.Position,
            ClientId = contact.ClientId, SupplierId = contact.SupplierId,
            CreatedAt = contact.CreatedAt
        };
    }
}

public class UpdateContactHandler : IRequestHandler<UpdateContactCommand, bool>
{
    private readonly ICrmDbContext _ctx;
    public UpdateContactHandler(ICrmDbContext ctx) => _ctx = ctx;

    public async Task<bool> Handle(UpdateContactCommand request, CancellationToken ct)
    {
        var contact = await _ctx.Contacts.FirstOrDefaultAsync(c => c.Id == request.Id, ct);
        if (contact == null) return false;

        contact.Name      = request.Name      ?? contact.Name;
        contact.Email     = request.Email     ?? contact.Email;
        contact.Phone     = request.Phone     ?? contact.Phone;
        contact.Position  = request.Position  ?? contact.Position;
        contact.ClientId  = request.ClientId;
        contact.SupplierId = request.SupplierId;

        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}

public class DeleteContactHandler : IRequestHandler<DeleteContactCommand, bool>
{
    private readonly ICrmDbContext _ctx;
    public DeleteContactHandler(ICrmDbContext ctx) => _ctx = ctx;

    public async Task<bool> Handle(DeleteContactCommand request, CancellationToken ct)
    {
        var contact = await _ctx.Contacts.FirstOrDefaultAsync(c => c.Id == request.Id, ct);
        if (contact == null) return false;
        _ctx.Contacts.Remove(contact);
        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}

/// <summary>
/// RGPD Art. 17 — Derecho de supresión.
/// Pseudoanonimiza los datos personales del contacto.
/// </summary>
public class AnonymizeContactHandler : IRequestHandler<AnonymizeContactCommand, bool>
{
    private readonly ICrmDbContext _ctx;
    public AnonymizeContactHandler(ICrmDbContext ctx) => _ctx = ctx;

    public async Task<bool> Handle(AnonymizeContactCommand request, CancellationToken ct)
    {
        var contact = await _ctx.Contacts.FirstOrDefaultAsync(c => c.Id == request.Id, ct);
        if (contact == null) return false;
        if (contact.IsAnonymized) return true;

        contact.Name         = "CONTACTO ANÓNIMO";
        contact.Email        = string.Empty;
        contact.Phone        = string.Empty;
        contact.Position     = string.Empty;
        contact.IsAnonymized = true;
        contact.AnonymizedAt = DateTime.UtcNow;

        _ctx.ActivityLogs.Add(new ActivityLog
        {
            Id = Guid.NewGuid(), CompanyId = contact.CompanyId,
            EntityType = "Contact", EntityId = contact.Id,
            Action = "Anonymized",
            Description = "Datos personales pseudoanonimizados (RGPD Art. 17)"
        });

        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}
