using Erp.Infrastructure;
using Erp.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Erp.Tests.Security;

/// <summary>
/// ADR-0018 #42c: ModuleAuthorizationFilter was only ever registered as
/// IAsyncAuthorizationFilter (interface-only), never as its own concrete type, so
/// options.Filters.AddService&lt;ModuleAuthorizationFilter&gt;() could never resolve it and
/// [RequiredModule] was silently never enforced at runtime. This guards the fix: the
/// concrete type must be resolvable directly from the DI container built by
/// AddInfrastructureServices, the same registration Program.cs relies on.
/// </summary>
public class ModuleAuthorizationFilterRegistrationTests
{
    [Fact]
    public void AddInfrastructureServices_RegistersModuleAuthorizationFilterAsConcreteType()
    {
        var services = new ServiceCollection();

        services.AddInfrastructureServices();

        Assert.Contains(services, sd => sd.ServiceType == typeof(ModuleAuthorizationFilter));
    }

    [Fact]
    public void AddInfrastructureServices_AlsoRegistersAbacAuthorizationFilterAsConcreteType()
    {
        var services = new ServiceCollection();

        services.AddInfrastructureServices();

        Assert.Contains(services, sd => sd.ServiceType == typeof(AbacAuthorizationFilter));
    }
}
