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

public class GetLeadsHandler : IRequestHandler<GetLeadsQuery, List<LeadDto>>
{
    private readonly ICrmDbContext _ctx;
    public GetLeadsHandler(ICrmDbContext ctx) => _ctx = ctx;

    public async Task<List<LeadDto>> Handle(GetLeadsQuery request, CancellationToken ct)
    {
        var query = _ctx.Leads.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(l => l.Name.Contains(request.Search)
                                  || l.Email.Contains(request.Search)
                                  || l.Notes.Contains(request.Search));

        return await query
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new LeadDto
            {
                Id = l.Id, Name = l.Name, Email = l.Email, Phone = l.Phone,
                Status = l.Status, Source = l.Source, Notes = l.Notes, CreatedAt = l.CreatedAt
            })
            .ToListAsync(ct);
    }
}

public class GetLeadByIdHandler : IRequestHandler<GetLeadByIdQuery, LeadDto?>
{
    private readonly ICrmDbContext _ctx;
    public GetLeadByIdHandler(ICrmDbContext ctx) => _ctx = ctx;

    public async Task<LeadDto?> Handle(GetLeadByIdQuery request, CancellationToken ct)
    {
        return await _ctx.Leads
            .Where(l => l.Id == request.Id)
            .Select(l => new LeadDto
            {
                Id = l.Id, Name = l.Name, Email = l.Email, Phone = l.Phone,
                Status = l.Status, Source = l.Source, Notes = l.Notes, CreatedAt = l.CreatedAt
            })
            .FirstOrDefaultAsync(ct);
    }
}

public class CreateLeadHandler : IRequestHandler<CreateLeadCommand, LeadDto>
{
    private readonly ICrmDbContext _ctx;
    private readonly ITenantContext _tenant;
    private readonly IPublisher _publisher;

    public CreateLeadHandler(ICrmDbContext ctx, ITenantContext tenant, IPublisher publisher)
    {
        _ctx = ctx; _tenant = tenant; _publisher = publisher;
    }

    public async Task<LeadDto> Handle(CreateLeadCommand request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new UnauthorizedAccessException("No tenant context.");

        var lead = new Lead
        {
            Id = Guid.NewGuid(), CompanyId = companyId,
            Name   = request.Name   ?? string.Empty,
            Email  = request.Email  ?? string.Empty,
            Phone  = request.Phone  ?? string.Empty,
            Status = string.IsNullOrWhiteSpace(request.Status) ? "New" : request.Status,
            Source = request.Source ?? string.Empty,
            Notes  = request.Notes  ?? string.Empty,
        };

        _ctx.Leads.Add(lead);
        _ctx.ActivityLogs.Add(new ActivityLog
        {
            Id = Guid.NewGuid(), CompanyId = companyId,
            EntityType = "Lead", EntityId = lead.Id,
            Action = "Created", Description = $"Lead '{lead.Name}' creado (Origen: {lead.Source})"
        });

        await _ctx.SaveChangesAsync(ct);

        await _publisher.Publish(new LeadCreatedEvent
        {
            LeadId = lead.Id, CompanyId = companyId,
            Name = lead.Name, Email = lead.Email, Source = lead.Source,
        }, ct);

        return new LeadDto
        {
            Id = lead.Id, Name = lead.Name, Email = lead.Email, Phone = lead.Phone,
            Status = lead.Status, Source = lead.Source, Notes = lead.Notes, CreatedAt = lead.CreatedAt
        };
    }
}

public class UpdateLeadHandler : IRequestHandler<UpdateLeadCommand, bool>
{
    private readonly ICrmDbContext _ctx;
    private readonly IPublisher _publisher;

    public UpdateLeadHandler(ICrmDbContext ctx, IPublisher publisher)
    {
        _ctx = ctx; _publisher = publisher;
    }

    public async Task<bool> Handle(UpdateLeadCommand request, CancellationToken ct)
    {
        var lead = await _ctx.Leads.FirstOrDefaultAsync(l => l.Id == request.Id, ct);
        if (lead == null) return false;

        var previousStatus = lead.Status;

        lead.Name   = request.Name   ?? lead.Name;
        lead.Email  = request.Email  ?? lead.Email;
        lead.Phone  = request.Phone  ?? lead.Phone;
        lead.Status = request.Status ?? lead.Status;
        lead.Source = request.Source ?? lead.Source;
        lead.Notes  = request.Notes  ?? lead.Notes;

        await _ctx.SaveChangesAsync(ct);

        if (request.Status != null && request.Status != previousStatus)
        {
            await _publisher.Publish(new LeadStatusChangedEvent
            {
                LeadId = lead.Id, CompanyId = lead.CompanyId,
                LeadName = lead.Name, PreviousStatus = previousStatus, NewStatus = lead.Status,
            }, ct);
        }

        return true;
    }
}

public class DeleteLeadHandler : IRequestHandler<DeleteLeadCommand, bool>
{
    private readonly ICrmDbContext _ctx;
    public DeleteLeadHandler(ICrmDbContext ctx) => _ctx = ctx;

    public async Task<bool> Handle(DeleteLeadCommand request, CancellationToken ct)
    {
        var lead = await _ctx.Leads.FirstOrDefaultAsync(l => l.Id == request.Id, ct);
        if (lead == null) return false;
        _ctx.Leads.Remove(lead);
        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}
