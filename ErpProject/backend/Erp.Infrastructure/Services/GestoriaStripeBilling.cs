using System.Text.Json;
using Erp.Application.Common.Interfaces;

namespace Erp.Infrastructure.Services;

/// <summary>
/// Helpers for Gestoría plan Stripe billing — line items separados por empresa (Fase 5).
/// </summary>
public static class GestoriaStripeBilling
{
    public const string CompanyIdMetadataKey = "gestoriaCompanyId";

    public static int ResolveBillableQuantity(IReadOnlyList<GestoriaCompanyBillingLine> companies)
        => Math.Max(1, companies.Count);

    /// <summary>
    /// Decide qué acción de Stripe (crear/actualizar cantidad/borrar) corresponde a cada
    /// `SubscriptionItem` al sincronizar una suscripción Gestoría multi-empresa. Extraído como
    /// función pura (sin llamadas a Stripe) precisamente porque antes esta lógica solo vivía
    /// entremezclada con el SDK de Stripe dentro de `StripeService.SyncGestoriaSubscriptionQuantityAsync`,
    /// y esa ruta (MaxCompanies&gt;1) no tenía ningún test — un fallo de orden de creación/borrado
    /// no lo habría detectado nadie (contra-auditoría jul 2026).
    /// </summary>
    public static IReadOnlyList<SubscriptionItemSyncAction> PlanSubscriptionItemSync(
        IReadOnlyDictionary<Guid, ExistingSubscriptionItem> existingByCompany,
        IReadOnlyList<GestoriaCompanyBillingLine> targetCompanies)
    {
        var plan = new List<SubscriptionItemSyncAction>();

        foreach (var company in targetCompanies)
        {
            if (existingByCompany.TryGetValue(company.CompanyId, out var existing))
            {
                if (existing.Quantity != 1)
                    plan.Add(SubscriptionItemSyncAction.Update(company, existing.ItemId));
            }
            else
            {
                plan.Add(SubscriptionItemSyncAction.Create(company));
            }
        }

        var targetIds = targetCompanies.Select(c => c.CompanyId).ToHashSet();
        foreach (var (companyId, existing) in existingByCompany)
        {
            if (!targetIds.Contains(companyId))
                plan.Add(SubscriptionItemSyncAction.Delete(companyId, existing.ItemId));
        }

        return plan;
    }

    public static string BuildCompanyLineDescription(GestoriaCompanyBillingLine company)
        => $"Gestoría — {company.Name} ({company.TaxId})";

    public static Dictionary<string, string> BuildCompanyItemMetadata(GestoriaCompanyBillingLine company) =>
        new()
        {
            [CompanyIdMetadataKey] = company.CompanyId.ToString(),
            ["companyName"] = company.Name,
            ["companyTaxId"] = company.TaxId,
        };

    public static Dictionary<string, string> BuildSubscriptionMetadata(
        Guid billingCompanyId,
        string planName,
        IReadOnlyList<GestoriaCompanyBillingLine> companies)
    {
        var metadata = new Dictionary<string, string>
        {
            ["companyId"] = billingCompanyId.ToString(),
            ["planName"] = planName,
        };

        if (companies.Count <= 1)
            return metadata;

        metadata["gestoriaCompanyCount"] = companies.Count.ToString();
        metadata["gestoriaBreakdown"] = TruncateSummary(companies);
        metadata["gestoriaBreakdownJson"] = JsonSerializer.Serialize(
            companies.Select(c => new { c.CompanyId, c.Name, c.TaxId }));
        return metadata;
    }

    public static string BuildCheckoutDescription(IReadOnlyList<GestoriaCompanyBillingLine> companies)
    {
        var qty = ResolveBillableQuantity(companies);
        return qty > 1
            ? $"Plan Gestoría — {qty} empresas activas (line item por empresa)"
            : "Plan Gestoría";
    }

    public static string? BuildInvoiceDescription(
        Stripe.Invoice invoice,
        IReadOnlyList<GestoriaCompanyBillingLine>? companies = null)
    {
        var lineItems = ParseLineItemsFromInvoice(invoice);
        if (lineItems.Count > 1)
        {
            var parts = lineItems.Select(l => $"{l.CompanyName}: {l.AmountEur:F2}€");
            return string.Join(" | ", parts);
        }

        var line = invoice.Lines?.Data?.FirstOrDefault();
        var qty = (int)(line?.Quantity ?? 1);
        var unitAmount = line?.Price?.UnitAmount ?? line?.Amount / Math.Max(qty, 1);
        var baseDesc = invoice.Description ?? line?.Description;

        if (qty > 1 && unitAmount is > 0)
        {
            var unitEur = unitAmount.Value / 100m;
            var qtyDesc = $"{qty} empresas × {unitEur:F2} {invoice.Currency?.ToUpperInvariant() ?? "EUR"}";
            baseDesc = string.IsNullOrWhiteSpace(baseDesc) ? qtyDesc : $"{baseDesc} ({qtyDesc})";
        }

        if (invoice.SubscriptionDetails?.Metadata?.TryGetValue("gestoriaBreakdown", out var breakdown) == true
            && !string.IsNullOrWhiteSpace(breakdown))
        {
            return string.IsNullOrWhiteSpace(baseDesc)
                ? $"Empresas: {breakdown}"
                : $"{baseDesc} | Empresas: {breakdown}";
        }

        if (companies is { Count: > 1 })
        {
            var names = string.Join(", ", companies.Select(c => c.Name));
            return string.IsNullOrWhiteSpace(baseDesc)
                ? $"Empresas: {names}"
                : $"{baseDesc} | Empresas: {names}";
        }

        return baseDesc;
    }

    public static IReadOnlyList<GestoriaCompanyBillingLine>? ParseBreakdownFromInvoice(Stripe.Invoice invoice)
    {
        var fromLines = ParseBreakdownFromLineItems(invoice);
        if (fromLines is { Count: > 0 })
            return fromLines;

        if (invoice.SubscriptionDetails?.Metadata?.TryGetValue("gestoriaBreakdownJson", out var json) == true
            && !string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<GestoriaCompanyJson>>(json);
                if (parsed is { Count: > 0 })
                {
                    return parsed
                        .Where(c => c.CompanyId != Guid.Empty)
                        .Select(c => new GestoriaCompanyBillingLine(c.CompanyId, c.Name ?? "", c.TaxId ?? ""))
                        .ToList();
                }
            }
            catch (JsonException)
            {
                // fall through
            }
        }

        return null;
    }

    public static IReadOnlyList<GestoriaInvoiceLineDto> ParseLineItemsFromInvoice(Stripe.Invoice invoice)
    {
        var lines = invoice.Lines?.Data ?? [];
        var result = new List<GestoriaInvoiceLineDto>();

        foreach (var line in lines)
        {
            var amountEur = line.Amount / 100m;
            var companyName = line.Metadata?.GetValueOrDefault("companyName")
                ?? line.Description
                ?? "Empresa";
            var companyId = Guid.TryParse(line.Metadata?.GetValueOrDefault(CompanyIdMetadataKey), out var id)
                ? id
                : Guid.Empty;
            var taxId = line.Metadata?.GetValueOrDefault("companyTaxId") ?? "";

            result.Add(new GestoriaInvoiceLineDto(companyId, companyName, taxId, amountEur, line.Description));
        }

        return result;
    }

    private static IReadOnlyList<GestoriaCompanyBillingLine>? ParseBreakdownFromLineItems(Stripe.Invoice invoice)
    {
        var items = ParseLineItemsFromInvoice(invoice);
        if (items.Count == 0) return null;

        return items
            .Where(i => i.CompanyId != Guid.Empty || !string.IsNullOrWhiteSpace(i.CompanyName))
            .Select(i => new GestoriaCompanyBillingLine(
                i.CompanyId != Guid.Empty ? i.CompanyId : Guid.NewGuid(),
                i.CompanyName,
                i.TaxId))
            .ToList();
    }

    private static string TruncateSummary(IReadOnlyList<GestoriaCompanyBillingLine> companies)
    {
        var summary = string.Join("; ", companies.Select(c => $"{c.Name} ({c.TaxId})"));
        return summary.Length > 450 ? summary[..447] + "..." : summary;
    }

    private sealed class GestoriaCompanyJson
    {
        public Guid CompanyId { get; set; }
        public string? Name { get; set; }
        public string? TaxId { get; set; }
    }
}

/// <summary>Proyección mínima de un `SubscriptionItem` de Stripe ya existente, sin acoplar
/// la lógica de planificación al tipo concreto del SDK.</summary>
public readonly record struct ExistingSubscriptionItem(string ItemId, long Quantity);

public enum SubscriptionItemSyncKind { Create, UpdateQuantity, Delete }

public sealed record SubscriptionItemSyncAction(
    SubscriptionItemSyncKind Kind,
    Guid CompanyId,
    string? ItemId,
    GestoriaCompanyBillingLine? Company)
{
    public static SubscriptionItemSyncAction Create(GestoriaCompanyBillingLine company) =>
        new(SubscriptionItemSyncKind.Create, company.CompanyId, null, company);

    public static SubscriptionItemSyncAction Update(GestoriaCompanyBillingLine company, string itemId) =>
        new(SubscriptionItemSyncKind.UpdateQuantity, company.CompanyId, itemId, company);

    public static SubscriptionItemSyncAction Delete(Guid companyId, string itemId) =>
        new(SubscriptionItemSyncKind.Delete, companyId, itemId, null);
}
