using Erp.Domain.Common;

namespace Erp.Domain.Entities.Api;

public class ApiUsageLog : BaseEntity
{
    public Guid ApiKeyId { get; set; }
    public ApiKey? ApiKey { get; set; }
    
    public string Endpoint { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
