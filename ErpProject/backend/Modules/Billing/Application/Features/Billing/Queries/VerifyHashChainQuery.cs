using MediatR;

namespace Erp.Modules.Billing.Application.Features.Billing.Queries;

public class HashChainErrorDto
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public int SequenceNumber { get; set; }
    public string? StoredHash { get; set; }
    public string ExpectedHash { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}

public class VerifyHashChainResult
{
    public bool Valid { get; set; }
    public string Series { get; set; } = string.Empty;
    public int FiscalYear { get; set; }
    public int Total { get; set; }
    public List<HashChainErrorDto> Errors { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

public class VerifyHashChainQuery : IRequest<VerifyHashChainResult>
{
    public string Series { get; set; } = string.Empty;
    public int FiscalYear { get; set; }
}
