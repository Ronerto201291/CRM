using System.ComponentModel.DataAnnotations;

namespace Erp.Infrastructure.Services;

public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    [Required(ErrorMessage = "Stripe:SecretKey is required. Set it via environment variable Stripe__SecretKey.")]
    public string SecretKey { get; init; } = string.Empty;

    [Required(ErrorMessage = "Stripe:WebhookSecret is required. Set it via environment variable Stripe__WebhookSecret.")]
    public string WebhookSecret { get; init; } = string.Empty;

    /// <summary>Publishable key used by the frontend. Optional at startup.</summary>
    public string PublishableKey { get; init; } = string.Empty;

    /// <summary>
    /// Stripe Price IDs for each paid plan.
    /// Set via Stripe__PriceIds__Starter, Stripe__PriceIds__Professional, Stripe__PriceIds__Enterprise.
    /// </summary>
    public StripePriceIds PriceIds { get; init; } = new();
}

public sealed class StripePriceIds
{
    public string Starter { get; init; } = string.Empty;
    public string Professional { get; init; } = string.Empty;
    public string Enterprise { get; init; } = string.Empty;

    public string GetByPlanName(string planName) =>
        planName switch
        {
            "Starter"      => Starter,
            "Professional" => Professional,
            "Enterprise"   => Enterprise,
            _ => throw new InvalidOperationException($"No Stripe price configured for plan '{planName}'")
        };
}
