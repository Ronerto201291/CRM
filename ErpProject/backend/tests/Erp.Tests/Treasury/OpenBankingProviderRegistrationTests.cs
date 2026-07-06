using Erp.Modules.Treasury.Application.Interfaces;
using Erp.Modules.Treasury.Infrastructure.Services.OpenBanking;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Erp.Tests.Treasury;

/// <summary>ADR-0018 #28 — selección proveedor PSD2 según entorno y credenciales.</summary>
public class OpenBankingProviderRegistrationTests
{
    private static ServiceProvider BuildProvider(IServiceCollection services)
    {
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddOpenBankingProvider_DevelopmentWithoutCredentials_RegistersMock()
    {
        var services = new ServiceCollection();
        var config = Config(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["OpenBanking:Provider"] = "Mock",
        });

        services.AddOpenBankingProvider(config);
        using var sp = BuildProvider(services);

        Assert.IsType<MockOpenBankingProvider>(sp.GetRequiredService<IOpenBankingProvider>());
    }

    [Fact]
    public void AddOpenBankingProvider_ProductionWithoutCredentials_RegistersStub()
    {
        var services = new ServiceCollection();
        var config = Config(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["OpenBanking:Provider"] = "Mock",
        });

        services.AddOpenBankingProvider(config);
        using var sp = BuildProvider(services);

        Assert.IsType<StubOpenBankingProvider>(sp.GetRequiredService<IOpenBankingProvider>());
    }

    [Fact]
    public void AddOpenBankingProvider_WithCredentials_RegistersConfigurableEvenIfProviderMock()
    {
        var services = new ServiceCollection();
        var config = Config(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["OpenBanking:Provider"] = "Mock",
            ["OpenBanking:ClientId"] = "secret_id_test",
            ["OpenBanking:ClientSecret"] = "secret_key_test",
            ["OpenBanking:ApiBaseUrl"] = "https://bankaccountdata.gocardless.com/api/v2",
        });

        services.AddOpenBankingProvider(config);
        using var sp = BuildProvider(services);

        Assert.IsType<ConfigurableOpenBankingProvider>(sp.GetRequiredService<IOpenBankingProvider>());
    }

    [Fact]
    public void AddOpenBankingProvider_ExplicitStubWithoutCredentials_RegistersStub()
    {
        var services = new ServiceCollection();
        var config = Config(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["OpenBanking:Provider"] = "Stub",
        });

        services.AddOpenBankingProvider(config);
        using var sp = BuildProvider(services);

        Assert.IsType<StubOpenBankingProvider>(sp.GetRequiredService<IOpenBankingProvider>());
    }

    [Theory]
    [InlineData("GoCardless")]
    [InlineData("Nordigen")]
    [InlineData("Configurable")]
    public void IsNamedConfigurableProvider_KnownProviders_ReturnsTrue(string name)
    {
        Assert.True(OpenBankingProviderRegistration.IsNamedConfigurableProvider(name));
    }

    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
