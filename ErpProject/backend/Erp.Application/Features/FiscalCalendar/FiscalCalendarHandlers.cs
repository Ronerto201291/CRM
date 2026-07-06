using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Application.Features.FiscalCalendar;

public sealed record FiscalEventDto(
    Guid Id,
    string ModelCode,
    string ModelName,
    int Year,
    int? Quarter,
    int? Month,
    string DeadlineDate,
    string ReminderDate,
    string Status,
    DateTime? SubmittedAt,
    string? SubmissionReference,
    decimal? Amount,
    string? Notes,
    int DiasRestantes,
    bool IsOverdue);

internal static class FiscalEventMapper
{
    public static FiscalEventDto ToDto(FiscalEvent e)
    {
        var today = DateTime.UtcNow.Date;
        return new FiscalEventDto(
            e.Id,
            e.ModelCode,
            e.ModelName,
            e.Year,
            e.Quarter,
            e.Month,
            e.DeadlineDate.ToString("yyyy-MM-dd"),
            e.ReminderDate.ToString("yyyy-MM-dd"),
            e.Status,
            e.SubmittedAt,
            e.SubmissionReference,
            e.Amount,
            e.Notes,
            (e.DeadlineDate.Date - today).Days,
            e.DeadlineDate.Date < today && e.Status != "Submitted");
    }
}

public record GetFiscalCalendarQuery(int? Year) : IRequest<IReadOnlyList<FiscalEventDto>>;

public class GetFiscalCalendarHandler : IRequestHandler<GetFiscalCalendarQuery, IReadOnlyList<FiscalEventDto>>
{
    private readonly IApplicationDbContext _ctx;

    public GetFiscalCalendarHandler(IApplicationDbContext ctx) => _ctx = ctx;

    public async Task<IReadOnlyList<FiscalEventDto>> Handle(GetFiscalCalendarQuery request, CancellationToken ct)
    {
        var year = request.Year ?? DateTime.UtcNow.Year;
        var events = await _ctx.FiscalEvents
            .Where(e => e.Year == year)
            .OrderBy(e => e.DeadlineDate)
            .AsNoTracking()
            .ToListAsync(ct);

        return events.Select(FiscalEventMapper.ToDto).ToList();
    }
}

public record GetFiscalEventQuery(Guid Id) : IRequest<FiscalEventDto?>;

public class GetFiscalEventHandler : IRequestHandler<GetFiscalEventQuery, FiscalEventDto?>
{
    private readonly IApplicationDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GetFiscalEventHandler(IApplicationDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<FiscalEventDto?> Handle(GetFiscalEventQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var evt = await _ctx.FiscalEvents
            .Where(e => e.Id == request.Id && e.CompanyId == tenantId)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        return evt is null ? null : FiscalEventMapper.ToDto(evt);
    }
}

public record GenerateFiscalCalendarCommand(int Year) : IRequest<GenerateFiscalCalendarResult>;

public sealed record GenerateFiscalCalendarResult(
    string Message,
    int TotalEvents,
    int NewEvents,
    int Skipped);

public class GenerateFiscalCalendarHandler : IRequestHandler<GenerateFiscalCalendarCommand, GenerateFiscalCalendarResult>
{
    private readonly IApplicationDbContext _ctx;
    private readonly IFiscalCalendarService _calendar;
    private readonly ITenantContext _tenant;

    public GenerateFiscalCalendarHandler(
        IApplicationDbContext ctx,
        IFiscalCalendarService calendar,
        ITenantContext tenant)
    {
        _ctx = ctx;
        _calendar = calendar;
        _tenant = tenant;
    }

    public async Task<GenerateFiscalCalendarResult> Handle(GenerateFiscalCalendarCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");

        var existing = await _ctx.FiscalEvents
            .Where(e => e.CompanyId == tenantId && e.Year == request.Year)
            .AsNoTracking()
            .Select(e => new { e.ModelCode, e.Quarter, e.Month })
            .ToListAsync(ct);

        var newEvents = await _calendar.GenerateYearCalendarAsync(tenantId, request.Year, ct);
        var added = 0;

        foreach (var evt in newEvents)
        {
            var alreadyExists = existing.Any(e =>
                e.ModelCode == evt.ModelCode
                && e.Quarter == evt.Quarter
                && e.Month == evt.Month);

            if (!alreadyExists)
            {
                _ctx.FiscalEvents.Add(evt);
                added++;
            }
        }

        await _ctx.SaveChangesAsync(ct);

        return new GenerateFiscalCalendarResult(
            $"Calendario fiscal {request.Year} generado correctamente",
            newEvents.Count,
            added,
            existing.Count);
    }
}

public record MarkFiscalEventSubmittedCommand(Guid Id, string? SubmissionReference) : IRequest<FiscalEventDto?>;

public class MarkFiscalEventSubmittedHandler : IRequestHandler<MarkFiscalEventSubmittedCommand, FiscalEventDto?>
{
    private readonly IApplicationDbContext _ctx;
    private readonly ITenantContext _tenant;

    public MarkFiscalEventSubmittedHandler(IApplicationDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<FiscalEventDto?> Handle(MarkFiscalEventSubmittedCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");

        var evt = await _ctx.FiscalEvents
            .FirstOrDefaultAsync(e => e.Id == request.Id && e.CompanyId == tenantId, ct);

        if (evt is null) return null;

        evt.Status = "Submitted";
        evt.SubmittedAt = DateTime.UtcNow;
        evt.SubmissionReference = request.SubmissionReference;

        await _ctx.SaveChangesAsync(ct);
        return FiscalEventMapper.ToDto(evt);
    }
}

public record GetOverdueFiscalEventsQuery : IRequest<IReadOnlyList<FiscalEventDto>>;

public class GetOverdueFiscalEventsHandler : IRequestHandler<GetOverdueFiscalEventsQuery, IReadOnlyList<FiscalEventDto>>
{
    private readonly IFiscalCalendarService _calendar;
    private readonly ITenantContext _tenant;

    public GetOverdueFiscalEventsHandler(IFiscalCalendarService calendar, ITenantContext tenant)
    {
        _calendar = calendar;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<FiscalEventDto>> Handle(GetOverdueFiscalEventsQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var overdue = await _calendar.GetOverdueEventsAsync(tenantId, ct);
        return overdue.Select(FiscalEventMapper.ToDto).ToList();
    }
}

public record CreateFiscalEventCommand(
    string ModelCode,
    string ModelName,
    int Year,
    int? Quarter,
    int? Month,
    DateTime DeadlineDate,
    DateTime ReminderDate,
    decimal? Amount,
    string? Notes) : IRequest<CreateFiscalEventResult>;

public sealed record CreateFiscalEventResult(Guid Id, string ModelName, DateTime DeadlineDate);

public class CreateFiscalEventHandler : IRequestHandler<CreateFiscalEventCommand, CreateFiscalEventResult>
{
    private readonly IApplicationDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CreateFiscalEventHandler(IApplicationDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<CreateFiscalEventResult> Handle(CreateFiscalEventCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");

        var evt = new FiscalEvent
        {
            Id = Guid.NewGuid(),
            CompanyId = tenantId,
            ModelCode = request.ModelCode,
            ModelName = request.ModelName,
            Year = request.Year,
            Quarter = request.Quarter,
            Month = request.Month,
            DeadlineDate = request.DeadlineDate,
            ReminderDate = request.ReminderDate,
            Status = "Pending",
            Amount = request.Amount,
            Notes = request.Notes,
            IsAutoGenerated = false,
            CreatedAt = DateTime.UtcNow
        };

        _ctx.FiscalEvents.Add(evt);
        await _ctx.SaveChangesAsync(ct);

        return new CreateFiscalEventResult(evt.Id, evt.ModelName, evt.DeadlineDate);
    }
}
