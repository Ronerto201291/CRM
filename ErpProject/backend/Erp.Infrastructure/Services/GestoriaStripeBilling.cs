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
