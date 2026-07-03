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
            new("38", "PSD2 / open banking", "requires_product_ok", "Contrato proveedor bancario + licencia AISP", "docs/adr/0019-producto-roadmap.md"),
            new("39", "Verifactu / RED producción", "blocked_external", "Homologación AEAT (#0b–#0d)", "docs/adr/0013-fiscal-sii-verifactu.md"),
            new("40", "Portal cliente B2B", "requires_product_ok", "Modelo UX + auth externa (magic link vs SSO)", "docs/adr/0019-producto-roadmap.md"),
            new("41", "TPV / cobro mostrador", "requires_product_ok", "Hardware + pasarela (Stripe Terminal u otro)", "docs/adr/0019-producto-roadmap.md"),
            new("42", "Servicios recurrentes", "requires_product_ok", "Modelo facturación recurrente y prorrateo", "docs/adr/0019-producto-roadmap.md"),
            new("42a", "Gestoría multi-empresa Fase 2+", "partial", "Fase 1 ✅; modelo suscripción gestoría pendiente OK", "docs/adr/0002-multitenancy-auth.md"),
            new("42b", "Multi-moneda avanzada", "roadmap_q3", "Prioridad producto Q3+", "docs/adr/0019-producto-roadmap.md"),
            new("42c", "IA OCR gastos", "roadmap_q3", "Proveedor OCR + coste por página", "docs/adr/0019-producto-roadmap.md"),
            new("42d", "Marketplace integraciones", "roadmap_q3", "Catálogo y revenue share sin definir", "docs/adr/0019-producto-roadmap.md"),
            new("42e", "App móvil nativa", "roadmap_q3", "PWA vs React Native sin decidir", "docs/adr/0019-producto-roadmap.md"),
            new("42f", "White-label / reseller", "roadmap_q3", "Modelo comercial reseller", "docs/adr/0019-producto-roadmap.md"),
        ];
        return Task.FromResult(items);
    }
}
