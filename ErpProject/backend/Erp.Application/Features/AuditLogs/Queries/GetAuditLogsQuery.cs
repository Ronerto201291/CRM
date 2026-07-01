using MediatR;

namespace Erp.Application.Features.AuditLogs.Queries;

public class AuditLogDto
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public DateTime Timestamp { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string UserEmail { get; set; } = string.Empty;
}

public class AuditLogsResult
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<AuditLogDto> Items { get; set; } = new();
}

public class GetAuditLogsQuery : IRequest<AuditLogsResult>
{
    public string? Action { get; set; }
    public string? EntityType { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
