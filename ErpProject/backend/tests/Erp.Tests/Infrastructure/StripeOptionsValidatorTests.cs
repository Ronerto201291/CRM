using Erp.Infrastructure.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Erp.Tests.Infrastructure;

/// <summary>ADR-0018 #0e — validación sk_test / sk_live.</summary>
public class StripeOptionsValidatorTests
{
    [Fact]
    public void Production_RejectsTestKey()
    {
        var validator = new StripeOptionsValidator(new HostEnvironment { EnvironmentName = "Production" });
        var result = validator.Validate(null, new StripeOptions { SecretKey = "sk_test_abc" });
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Development_RejectsLiveKey()
    {
        var validator = new StripeOptionsValidator(new HostEnvironment { EnvironmentName = "Development" });
        var result = validator.Validate(null, new StripeOptions { SecretKey = "sk_live_abc" });
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Development_AcceptsTestKey()
    {
        var validator = new StripeOptionsValidator(new HostEnvironment { EnvironmentName = "Development" });
        var result = validator.Validate(null, new StripeOptions { SecretKey = "sk_test_dummy" });
        Assert.True(result.Succeeded);
    }

    private sealed class HostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Erp.Tests";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
