using Erp.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.Subscriptions;

/// <summary>
/// Stripe webhook receiver. Excluded from authentication and tenant middleware.
/// Webhook signature is verified by Stripe-Signature header (HMAC-SHA256).
///
/// Configure in Stripe Dashboard → Developers → Webhooks:
///   Endpoint URL : https://yourdomain.com/api/stripe/webhook
///   Events       : checkout.session.completed
///                  customer.subscription.updated
///                  customer.subscription.deleted
///                  invoice.payment_succeeded
///                  invoice.payment_failed
/// </summary>
[ApiController]
[Route("api/stripe")]
[AllowAnonymous]
public class StripeWebhookController : ControllerBase
{
    private readonly StripeService _stripeService;
    private readonly ILogger<StripeWebhookController> _logger;

    public StripeWebhookController(StripeService stripeService, ILogger<StripeWebhookController> logger)
    {
        _stripeService = stripeService;
        _logger = logger;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> HandleWebhook(CancellationToken ct)
    {
        var payload = await new StreamReader(Request.Body).ReadToEndAsync(ct);
        var stripeSignature = Request.Headers["Stripe-Signature"].FirstOrDefault();

        if (string.IsNullOrEmpty(stripeSignature))
            return BadRequest(new { error = "Missing Stripe-Signature header" });

        try
        {
            await _stripeService.HandleWebhookAsync(payload, stripeSignature, ct);
            return Ok(new { received = true });
        }
        catch (Stripe.StripeException ex)
        {
            _logger.LogWarning("Stripe webhook validation failed: {Message}", ex.Message);
            return BadRequest(new { error = "Webhook signature validation failed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Stripe webhook");
            return StatusCode(500);
        }
    }
}
