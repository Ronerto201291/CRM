using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Core;

namespace Erp.Tests.TestSupport;

public sealed class FakeFiscalCalendarService : IFiscalCalendarService
{
    public List<FiscalEvent> GeneratedEvents { get; set; } =
    [
        new FiscalEvent
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.Empty,
            ModelCode = "303",
            ModelName = "IVA trimestral",
            Year = 2026,
            Quarter = 1,
            DeadlineDate = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc),
            ReminderDate = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
            Status = "Pending",
        },
        new FiscalEvent
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.Empty,
            ModelCode = "111",
            ModelName = "Retenciones",
            Year = 2026,
            Quarter = 1,
            DeadlineDate = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc),
            ReminderDate = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
            Status = "Pending",
        },
    ];

    public List<FiscalEvent> OverdueEvents { get; set; } = [];

    public Task<List<FiscalEvent>> GenerateYearCalendarAsync(Guid companyId, int year, CancellationToken ct = default)
    {
        foreach (var evt in GeneratedEvents)
            evt.CompanyId = companyId;
        return Task.FromResult(GeneratedEvents.Select(e => CloneForCompany(e, companyId)).ToList());
    }

    public Task<List<FiscalEvent>> GetPendingEventsAsync(Guid companyId, int year, CancellationToken ct = default)
        => Task.FromResult(GeneratedEvents.Where(e => e.CompanyId == companyId && e.Status == "Pending").ToList());

    public Task<List<FiscalEvent>> GetOverdueEventsAsync(Guid companyId, CancellationToken ct = default)
    {
        foreach (var evt in OverdueEvents)
            evt.CompanyId = companyId;
        return Task.FromResult(OverdueEvents.ToList());
    }

    public Task<FiscalEvent> MarkAsSubmittedAsync(Guid eventId, string? submissionReference, CancellationToken ct = default)
    {
        var evt = GeneratedEvents.FirstOrDefault(e => e.Id == eventId)
            ?? throw new KeyNotFoundException("Evento no encontrado");
        evt.Status = "Submitted";
        evt.SubmittedAt = DateTime.UtcNow;
        evt.SubmissionReference = submissionReference;
        return Task.FromResult(evt);
    }

    private static FiscalEvent CloneForCompany(FiscalEvent source, Guid companyId) => new()
    {
        Id = Guid.NewGuid(),
        CompanyId = companyId,
        ModelCode = source.ModelCode,
        ModelName = source.ModelName,
        Year = source.Year,
        Quarter = source.Quarter,
        Month = source.Month,
        DeadlineDate = source.DeadlineDate,
        ReminderDate = source.ReminderDate,
        Status = source.Status,
    };
}
