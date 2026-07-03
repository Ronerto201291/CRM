using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Erp.Infrastructure.Services;

/// <summary>ADR-0018 #0e — sk_test en no-producción, sk_live en producción.</summary>
public sealed class StripeOptionsValidator : IValidateOptions<StripeOptions>
{
    private readonly IHostEnvironment _environment;

    public StripeOptionsValidator(IHostEnvironment environment) => _environment = environment;

    public ValidateOptionsResult Validate(string? name, StripeOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SecretKey))
            return ValidateOptionsResult.Fail("Stripe:SecretKey is required.");

        var isProduction = _environment.IsProduction();
        var key = options.SecretKey.Trim();

        if (isProduction && !key.StartsWith("sk_live_", StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail(
                "Stripe:SecretKey debe usar prefijo sk_live_ en entorno Production.");
        }

        if (!isProduction && !key.StartsWith("sk_test_", StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail(
                "Stripe:SecretKey debe usar prefijo sk_test_ fuera de Production.");
        }

        if (!string.IsNullOrWhiteSpace(options.PublishableKey))
        {
            var pk = options.PublishableKey.Trim();
            var expectedPkPrefix = isProduction ? "pk_live_" : "pk_test_";
            if (!pk.StartsWith(expectedPkPrefix, StringComparison.Ordinal))
            {
                return ValidateOptionsResult.Fail(
                    $"Stripe:PublishableKey debe usar prefijo {expectedPkPrefix} en este entorno.");
            }
        }

        return ValidateOptionsResult.Success;
    }
}
