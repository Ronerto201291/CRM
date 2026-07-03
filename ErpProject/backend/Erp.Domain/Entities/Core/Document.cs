using Erp.Domain.Common;

namespace Erp.Domain.Entities.Core;

/// <summary>
/// Biblioteca interna de documentos (ADR-0018 #42d). Vive en core, no en un
/// módulo concreto, porque un documento puede enlazarse a entidades de
/// distintos módulos (Client/Supplier de CRM, Invoice de Billing, Expense de
/// Expenses...). Para no violar la dirección de dependencias (core no
/// referencia módulos), el enlace es genérico vía EntityType/EntityId en vez
/// de una FK real — mismo patrón ya usado en Treasury (PaymentOrder.SourceType
/// / SourceId).
/// </summary>
public class Document : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid UploadedByUserId { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }

    /// <summary>Clave del objeto en el almacenamiento (IFileStorageService).</summary>
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>Entidad opcional a la que va enlazado, p. ej. "Client", "Supplier",
    /// "Invoice", "Expense". Null si es un documento suelto (sin enlazar).</summary>
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }

    public string? Description { get; set; }
}
