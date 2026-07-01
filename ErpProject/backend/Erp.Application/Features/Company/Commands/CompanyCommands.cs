using MediatR;

namespace Erp.Application.Features.Company.Commands;

public class UpdateCompanyCommand : IRequest<bool>
{
    public string? Name { get; set; }
    public string? TaxId { get; set; }
    public string? Address { get; set; }
    public bool QrUploadEnabled { get; set; } = true;
}

public class RegenerateTokenCommand : IRequest<string>
{
}
