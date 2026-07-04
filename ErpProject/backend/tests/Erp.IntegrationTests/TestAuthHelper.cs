using System.Net.Http.Headers;
using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Security;
using Microsoft.Extensions.Configuration;

namespace Erp.IntegrationTests;

/// <summary>
/// Genera JWT válidos para tests HTTP con WebApplicationFactory (ADR-0018 #32).
/// Debe coincidir con appsettings.IntegrationTests.json.
/// </summary>
public static class TestAuthHelper
{
    public const string JwtSecret = "integration-test-secret-min-32-chars!!";
    public const string JwtIssuer = "Erp.Api";
    public const string JwtAudience = "Erp.Client";

    public static string CreateToken(
        Guid userId,
        string email,
        Guid companyId,
        string firstName = "Test",
        string lastName = "User")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = JwtSecret,
                ["Jwt:Issuer"] = JwtIssuer,
                ["Jwt:Audience"] = JwtAudience,
            })
            .Build();

        var user = new User
        {
            Id = userId,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            CompanyId = companyId,
        };

        return new JwtProvider(config).Generate(user, companyId);
    }

    public static void ApplyAuth(HttpClient client, string token, Guid tenantId)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
    }

    public static HttpClient CreateAuthenticatedClient(HttpClient baseClient, string token, Guid tenantId)
    {
        var client = baseClient;
        ApplyAuth(client, token, tenantId);
        return client;
    }
}
