using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Erp.Infrastructure.Services;

/// <summary>
/// Ensures Stripe API keys match the deployment environment (sk_test_ vs sk_live_).
/// </summary>
public sealed class StripeOptionsValidator : IValidateOptions<StripeOptions>
{
    private readonly IHostEnvironment _environment;

    public StripeOptionsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, StripeOptions options)
    {
        var key = options.SecretKey;
        if (string.IsNullOrWhiteSpace(key))
            return ValidateOptionsResult.Fail("Stripe:SecretKey is required.");

        var isTestKey = key.StartsWith("sk_test_", StringComparison.Ordinal);
        var isLiveKey = key.StartsWith("sk_live_", StringComparison.Ordinal);

        if (!isTestKey && !isLiveKey)
        {
            return ValidateOptionsResult.Fail(
                "Stripe:SecretKey must start with sk_test_ or sk_live_.");
        }

        if (_environment.IsProduction() && isTestKey)
        {
            return ValidateOptionsResult.Fail(
                "Production environment requires a live Stripe key (sk_live_).");
        }

        if (!_environment.IsProduction() && isLiveKey)
        {
            return ValidateOptionsResult.Fail(
                "Non-production environments must use a test Stripe key (sk_test_).");
        }

        return ValidateOptionsResult.Success;
    }
}
