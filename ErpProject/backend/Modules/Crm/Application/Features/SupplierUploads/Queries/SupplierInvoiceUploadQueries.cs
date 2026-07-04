using MediatR;

namespace Erp.Modules.Crm.Application.Features.SupplierUploads.Queries;

public class SupplierInvoiceUploadDto
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public DateTime UploadedAt { get; set; }
}

/// <summary>Listado global para el staff (mismo patrón que GetExpenseUploadsQuery).</summary>
public class GetSupplierInvoiceUploadsQuery : IRequest<List<SupplierInvoiceUploadDto>> { }

public class GetSupplierInvoiceUploadDownloadUrlQuery : IRequest<string>
{
    public Guid Id { get; set; }
}
