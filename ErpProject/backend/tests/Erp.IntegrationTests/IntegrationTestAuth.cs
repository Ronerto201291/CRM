using System.Net;
using System.Net.Http.Json;
using Erp.Application.Features.ApiKeys.Commands;
using Erp.Application.Features.Auth.Commands;
using Xunit;

namespace Erp.IntegrationTests;

/// <summary>
/// Punto único de verdad para auth HTTP en integración Postgres (ADR-0018 #32, #42c).
/// RegisterCompany deja plan Free con pocos módulos; los tests que ejercitan endpoints con
/// [RequiredModule] deben usar <see cref="RegisterEnterpriseAsync"/>.
/// </summary>
public static class IntegrationTestAuth
{
    public sealed record Session(
        HttpClient Client,
        Guid CompanyId,
        Guid UserId,
        string Email,
        string Token,
        string? ApiKey = null)
    {
        public void Deconstruct(out HttpClient client, out Guid companyId)
        {
            client = Client;
            companyId = CompanyId;
        }
    }

    /// <summary>
    /// Registro real → upgrade Enterprise → habilitar todos los módulos del plan → JWT
    /// (+ opcional X-Api-Key). Admin ya tiene permisos vía <c>SeedDefaultRolePermissionsHandler</c>
    /// en el <c>CompanyCreatedEvent</c> del registro.
    /// </summary>
    public static async Task<Session> RegisterEnterpriseAsync(
        PostgresWebApplicationFactory factory,
        string emailPrefix,
        bool withApiKey = false,
        string? companyName = null)
    {
        var body = await RegisterFreeAsync(factory, emailPrefix, companyName);

        await IntegrationTestModuleHelper.UpgradeToEnterpriseAndEnableAllModulesAsync(
            factory.GetConnectionString(), body.CompanyId);

        var authedClient = factory.CreatePostgresClient();
        TestAuthHelper.ApplyAuth(authedClient, body.Token, body.CompanyId);

        string? apiKey = null;
        if (withApiKey)
        {
            var keyResponse = await authedClient.PostAsJsonAsync("/api/apikeys", new CreateApiKeyCommand
            {
                Name = "Integration Test",
                RateLimit = 500,
            });
            Assert.Equal(HttpStatusCode.OK, keyResponse.StatusCode);
            var keyBody = await keyResponse.Content.ReadFromJsonAsync<CreateApiKeyResult>();
            Assert.NotNull(keyBody);
            apiKey = keyBody!.RawKey;
            authedClient.DefaultRequestHeaders.Remove("X-Api-Key");
            authedClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        }

        return new Session(authedClient, body.CompanyId, body.UserId, body.Email, body.Token, apiKey);
    }

    /// <summary>
    /// Solo registro Free (sin upgrade). Usar cuando el test valida el plan Free o un cambio
    /// de plan posterior (p. ej. webhook Stripe) o aislamiento multi-tenant en CRM/Billing.
    /// </summary>
    public static async Task<RegisterCompanyResponse> RegisterFreeAsync(
        PostgresWebApplicationFactory factory,
        string emailPrefix,
        string? companyName = null)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var client = factory.CreatePostgresClient();

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterCompanyCommand
        {
            CompanyName = companyName ?? $"Integration Co {suffix}",
            CompanyTaxId = IntegrationTestRegistration.NextTaxId(),
            CompanyAddress = "Calle Test 1",
            AdminEmail = $"{emailPrefix}-{suffix}@test.local",
            AdminPassword = "SecurePass1!",
            AdminFirstName = "Admin",
            AdminLastName = "Test",
        });

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        var body = await registerResponse.Content.ReadFromJsonAsync<RegisterCompanyResponse>();
        Assert.NotNull(body);
        return body!;
    }
}
