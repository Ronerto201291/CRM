using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Erp.Application.Features.Auth.Commands;
using Stripe;
using Xunit;

namespace Erp.IntegrationTests;

/// <summary>
/// POST /api/stripe/webhook con firma HMAC válida (checkout.session.completed).
/// </summary>
public class StripeWebhookHttpTests : IClassFixture<PostgresWebApplicationFactory>
{
    private const string WebhookSecret = "whsec_test_webhook_secret";

    private readonly PostgresWebApplicationFactory _factory;

    public StripeWebhookHttpTests(PostgresWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task StripeWebhook_CheckoutSessionCompleted_Returns200WithValidSignature()
    {
        if (!_factory.DockerAvailable)
            return;

        var companyId = await RegisterCompanyAsync();
        await EnsureProPlanAsync();

        var apiVersion = StripeConfiguration.ApiVersion;
        var payload = "{\"id\":\"evt_integration_checkout_completed\",\"object\":\"event\",\"api_version\":\"" + apiVersion
            + "\",\"created\":1710000000,\"type\":\"checkout.session.completed\",\"livemode\":false,\"pending_webhooks\":1,"
            + "\"request\":{\"id\":null,\"idempotency_key\":null},\"data\":{\"object\":{\"id\":\"cs_test_integration\","
            + "\"object\":\"checkout.session\",\"mode\":\"subscription\",\"payment_status\":\"paid\",\"status\":\"complete\","
            + "\"currency\":\"eur\",\"subscription\":\"sub_test_integration\",\"metadata\":{\"companyId\":\""
            + companyId + "\",\"planName\":\"Pro\"}}}}";

        var signature = StripeWebhookTestSignature.GenerateHeader(payload, WebhookSecret);

        var client = _factory.CreatePostgresClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/stripe/webhook")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Stripe-Signature", signature);

        var response = await client.SendAsync(request);
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Webhook → {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("received", body, StringComparison.OrdinalIgnoreCase);

        await using var conn = new Npgsql.NpgsqlConnection(_factory.GetConnectionString());
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM "Subscriptions" WHERE "CompanyId" = @cid AND "PlanName" = 'Pro'
            """;
        cmd.Parameters.Add(new Npgsql.NpgsqlParameter("cid", companyId));
        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task StripeWebhook_InvalidSignature_Returns400()
    {
        if (!_factory.DockerAvailable)
            return;

        var client = _factory.CreatePostgresClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/stripe/webhook")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Stripe-Signature", "t=0,v1=invalid");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<Guid> RegisterCompanyAsync()
    {
        var client = _factory.CreatePostgresClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterCompanyCommand
        {
            CompanyName = $"Stripe Webhook Co {suffix}",
            CompanyTaxId = IntegrationTestRegistration.NextTaxId(),
            CompanyAddress = "Calle Test 1",
            AdminEmail = $"stripe-wh-{suffix}@test.local",
            AdminPassword = "SecurePass1!",
            AdminFirstName = "Admin",
            AdminLastName = "Test",
        });
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        var body = await registerResponse.Content.ReadFromJsonAsync<RegisterCompanyResponse>();
        return body!.CompanyId;
    }

    private async Task EnsureProPlanAsync()
    {
        await using var conn = new Npgsql.NpgsqlConnection(_factory.GetConnectionString());
        await conn.OpenAsync();

        await using (var planCmd = conn.CreateCommand())
        {
            planCmd.CommandText = """
                INSERT INTO "Plans" ("Id", "Name", "Description", "MonthlyPrice", "YearlyPrice", "MaxUsers", "MaxInvoicesPerMonth", "IsActive", "SortOrder")
                SELECT @pid, 'Pro', 'Plan Pro test', 49, 490, 10, 500, true, 2
                WHERE NOT EXISTS (SELECT 1 FROM "Plans" WHERE "Name" = 'Pro')
                """;
            planCmd.Parameters.Add(new Npgsql.NpgsqlParameter("pid", Guid.NewGuid()));
            await planCmd.ExecuteNonQueryAsync();
        }

        await using (var moduleCmd = conn.CreateCommand())
        {
            moduleCmd.CommandText = """
                INSERT INTO "PlanModules" ("Id", "PlanId", "ModuleName", "IsIncluded")
                SELECT @mid, p."Id", 'Billing', true
                FROM "Plans" p
                WHERE p."Name" = 'Pro'
                  AND NOT EXISTS (
                    SELECT 1 FROM "PlanModules" pm
                    WHERE pm."PlanId" = p."Id" AND pm."ModuleName" = 'Billing'
                  )
                """;
            moduleCmd.Parameters.Add(new Npgsql.NpgsqlParameter("mid", Guid.NewGuid()));
            await moduleCmd.ExecuteNonQueryAsync();
        }
    }
}

internal static class StripeWebhookTestSignature
{
    public static string GenerateHeader(string payload, string webhookSecret, long? timestamp = null)
    {
        var ts = timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signedPayload = $"{ts}.{payload}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhookSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
        return $"t={ts},v1={BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant()}";
    }
}
