using MediatR;

namespace Erp.Modules.Crm.Application.Features.SupplierUploads.Commands;

public record UploadSupplierInvoiceByTokenResult(Guid UploadId, string Message);

/// <summary>Sube una factura de proveedor vía enlace público (ADR-0018 #39, mismo patrón que UploadExpenseByTokenCommand).</summary>
public class UploadSupplierInvoiceByTokenCommand : IRequest<UploadSupplierInvoiceByTokenResult>
{
    public string Token { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] FileContent { get; set; } = [];
    public string? Comment { get; set; }
}

/// <summary>El staff marca una factura subida como revisada (ya gestionada manualmente en Purchasing).</summary>
public class MarkSupplierInvoiceUploadReviewedCommand : IRequest<bool>
{
    public Guid Id { get; set; }
}
