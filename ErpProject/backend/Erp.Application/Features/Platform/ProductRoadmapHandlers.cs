using MediatR;

namespace Erp.Application.Features.Platform;

public record ProductRoadmapItemDto(
    string Id,
    string Title,
    string Status,
    string Blocker,
    string? DesignDoc);

public record GetProductRoadmapQuery : IRequest<IReadOnlyList<ProductRoadmapItemDto>>;

/// <summary>ADR-0018 #38–#42f — roadmap honesto; no implementa features bloqueadas por producto.</summary>
public sealed class GetProductRoadmapHandler : IRequestHandler<GetProductRoadmapQuery, IReadOnlyList<ProductRoadmapItemDto>>
{
    public Task<IReadOnlyList<ProductRoadmapItemDto>> Handle(GetProductRoadmapQuery request, CancellationToken ct)
    {
        IReadOnlyList<ProductRoadmapItemDto> items =
        [
            new("38", "Multi-moneda Billing↔Treasury", "implemented", "Homologación FacturaE multi-divisa pendiente", "docs/adr/0019-producto-roadmap.md"),
            new("39", "Verifactu / RED producción", "blocked_external", "Homologación AEAT (#0b–#0d)", "docs/adr/0013-fiscal-sii-verifactu.md"),
            new("40", "IA asistida (anomalías, previsión, categorización)", "implemented", "Heurísticas sin LLM externo", "docs/adr/0018-calidad-arquitectura.md"),
            new("41", "Onboarding guiado (sector + CSV clientes)", "implemented", "Import Excel completo pendiente", "docs/adr/0018-calidad-arquitectura.md"),
            new("42", "Notificaciones proactivas", "implemented", "Push móvil pendiente", "docs/adr/0015-automatizacion.md"),
            new("42a", "Gestoría multi-empresa", "partial", "Fases 1–5 ✅ quantity Stripe por empresa + sync webhooks; homologación PDF multi-línea opcional", "docs/adr/0002-multitenancy-auth.md"),
            new("42b", "Conciliación TPV/Bizum/caja", "implemented", "ADR-0018 #42b", "docs/adr/0019-producto-roadmap.md"),
            new("42c", "Módulo × permiso", "implemented", "ADR-0018 #42c", "docs/adr/0019-producto-roadmap.md"),
            new("42d", "Biblioteca documentos", "partial", "Entidad Document existe; integraciones pendientes", "docs/adr/0019-producto-roadmap.md"),
            new("42e", "Export periódico gestoría", "implemented", "ZIP: libros IVA + asientos CSV + PDFs emitidas/gastos + LEEME", "docs/adr/0019-producto-roadmap.md"),
            new("42f", "Servicios recurrentes cliente", "implemented", "ADR-0018 #42f", "docs/adr/0019-producto-roadmap.md"),
        ];
        return Task.FromResult(items);
    }
}
