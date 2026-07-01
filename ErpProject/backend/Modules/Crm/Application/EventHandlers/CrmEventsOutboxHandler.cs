using Erp.Application.Common.Events;
using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Outbox;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Erp.Modules.Crm.Application.EventHandlers;

/// <summary>
/// Bridges all CRM domain events to the Outbox table so they can be reliably
/// relayed to external consumers (email, webhooks, notifications, etc.)
/// without tight coupling between modules.
/// </summary>

public class LeadCreatedOutboxHandler : INotificationHandler<LeadCreatedEvent>
{
    private readonly IApplicationDbContext _appCtx;
    private readonly ILogger<LeadCreatedOutboxHandler> _logger;

    public LeadCreatedOutboxHandler(IApplicationDbContext appCtx, ILogger<LeadCreatedOutboxHandler> logger)
    { _appCtx = appCtx; _logger = logger; }

    public async Task Handle(LeadCreatedEvent notification, CancellationToken ct)
    {
        _appCtx.OutboxMessages.Add(new OutboxMessage
        {
            Id        = Guid.NewGuid(),
            CompanyId = notification.CompanyId,
            EventType = nameof(LeadCreatedEvent),
            Payload   = JsonSerializer.Serialize(new
            {
                notification.LeadId,
                notification.CompanyId,
                notification.Name,
                notification.Email,
                notification.Source,
                notification.OccurredOn,
            }),
            CreatedAt = notification.OccurredOn,
        });
        await _appCtx.SaveChangesAsync(ct);
        _logger.LogInformation("LeadCreated queued to Outbox: lead={LeadId}, company={CompanyId}",
            notification.LeadId, notification.CompanyId);
    }
}

public class LeadStatusChangedOutboxHandler : INotificationHandler<LeadStatusChangedEvent>
{
    private readonly IApplicationDbContext _appCtx;
    private readonly ILogger<LeadStatusChangedOutboxHandler> _logger;

    public LeadStatusChangedOutboxHandler(IApplicationDbContext appCtx, ILogger<LeadStatusChangedOutboxHandler> logger)
    { _appCtx = appCtx; _logger = logger; }

    public async Task Handle(LeadStatusChangedEvent notification, CancellationToken ct)
    {
        _appCtx.OutboxMessages.Add(new OutboxMessage
        {
            Id        = Guid.NewGuid(),
            CompanyId = notification.CompanyId,
            EventType = nameof(LeadStatusChangedEvent),
            Payload   = JsonSerializer.Serialize(new
            {
                notification.LeadId,
                notification.CompanyId,
                notification.LeadName,
                notification.PreviousStatus,
                notification.NewStatus,
                notification.OccurredOn,
            }),
            CreatedAt = notification.OccurredOn,
        });
        await _appCtx.SaveChangesAsync(ct);
        _logger.LogInformation(
            "LeadStatusChanged queued to Outbox: lead={LeadId} {From}→{To}",
            notification.LeadId, notification.PreviousStatus, notification.NewStatus);
    }
}

public class ClientCreatedOutboxHandler : INotificationHandler<ClientCreatedEvent>
{
    private readonly IApplicationDbContext _appCtx;
    private readonly ILogger<ClientCreatedOutboxHandler> _logger;

    public ClientCreatedOutboxHandler(IApplicationDbContext appCtx, ILogger<ClientCreatedOutboxHandler> logger)
    { _appCtx = appCtx; _logger = logger; }

    public async Task Handle(ClientCreatedEvent notification, CancellationToken ct)
    {
        _appCtx.OutboxMessages.Add(new OutboxMessage
        {
            Id        = Guid.NewGuid(),
            CompanyId = notification.CompanyId,
            EventType = nameof(ClientCreatedEvent),
            Payload   = JsonSerializer.Serialize(new
            {
                notification.ClientId,
                notification.CompanyId,
                notification.Name,
                notification.Email,
                notification.TaxId,
                notification.OccurredOn,
            }),
            CreatedAt = notification.OccurredOn,
        });
        await _appCtx.SaveChangesAsync(ct);
        _logger.LogInformation("ClientCreated queued to Outbox: client={ClientId}, company={CompanyId}",
            notification.ClientId, notification.CompanyId);
    }
}

public class ClientUpdatedOutboxHandler : INotificationHandler<ClientUpdatedEvent>
{
    private readonly IApplicationDbContext _appCtx;
    private readonly ILogger<ClientUpdatedOutboxHandler> _logger;

    public ClientUpdatedOutboxHandler(IApplicationDbContext appCtx, ILogger<ClientUpdatedOutboxHandler> logger)
    { _appCtx = appCtx; _logger = logger; }

    public async Task Handle(ClientUpdatedEvent notification, CancellationToken ct)
    {
        _appCtx.OutboxMessages.Add(new OutboxMessage
        {
            Id        = Guid.NewGuid(),
            CompanyId = notification.CompanyId,
            EventType = nameof(ClientUpdatedEvent),
            Payload   = JsonSerializer.Serialize(new
            {
                notification.ClientId,
                notification.CompanyId,
                notification.Name,
                notification.OccurredOn,
            }),
            CreatedAt = notification.OccurredOn,
        });
        await _appCtx.SaveChangesAsync(ct);
        _logger.LogInformation("ClientUpdated queued to Outbox: client={ClientId}, company={CompanyId}",
            notification.ClientId, notification.CompanyId);
    }
}

public class SupplierCreatedOutboxHandler : INotificationHandler<SupplierCreatedEvent>
{
    private readonly IApplicationDbContext _appCtx;
    private readonly ILogger<SupplierCreatedOutboxHandler> _logger;

    public SupplierCreatedOutboxHandler(IApplicationDbContext appCtx, ILogger<SupplierCreatedOutboxHandler> logger)
    { _appCtx = appCtx; _logger = logger; }

    public async Task Handle(SupplierCreatedEvent notification, CancellationToken ct)
    {
        _appCtx.OutboxMessages.Add(new OutboxMessage
        {
            Id        = Guid.NewGuid(),
            CompanyId = notification.CompanyId,
            EventType = nameof(SupplierCreatedEvent),
            Payload   = JsonSerializer.Serialize(new
            {
                notification.SupplierId,
                notification.CompanyId,
                notification.Name,
                notification.TaxId,
                notification.OccurredOn,
            }),
            CreatedAt = notification.OccurredOn,
        });
        await _appCtx.SaveChangesAsync(ct);
        _logger.LogInformation("SupplierCreated queued to Outbox: supplier={SupplierId}, company={CompanyId}",
            notification.SupplierId, notification.CompanyId);
    }
}

public class ContactCreatedOutboxHandler : INotificationHandler<ContactCreatedEvent>
{
    private readonly IApplicationDbContext _appCtx;
    private readonly ILogger<ContactCreatedOutboxHandler> _logger;

    public ContactCreatedOutboxHandler(IApplicationDbContext appCtx, ILogger<ContactCreatedOutboxHandler> logger)
    { _appCtx = appCtx; _logger = logger; }

    public async Task Handle(ContactCreatedEvent notification, CancellationToken ct)
    {
        _appCtx.OutboxMessages.Add(new OutboxMessage
        {
            Id        = Guid.NewGuid(),
            CompanyId = notification.CompanyId,
            EventType = nameof(ContactCreatedEvent),
            Payload   = JsonSerializer.Serialize(new
            {
                notification.ContactId,
                notification.CompanyId,
                notification.Name,
                notification.Email,
                notification.ClientId,
                notification.SupplierId,
                notification.OccurredOn,
            }),
            CreatedAt = notification.OccurredOn,
        });
        await _appCtx.SaveChangesAsync(ct);
        _logger.LogInformation("ContactCreated queued to Outbox: contact={ContactId}, company={CompanyId}",
            notification.ContactId, notification.CompanyId);
    }
}
