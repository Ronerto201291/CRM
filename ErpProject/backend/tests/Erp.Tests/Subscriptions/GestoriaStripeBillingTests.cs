using Erp.Infrastructure.Services;
using Erp.Infrastructure.Services.Ai;
using Erp.Application.Common.Interfaces;
using Erp.Application.Options;
using Microsoft.Extensions.Options;
using Xunit;

namespace Erp.Tests.Subscriptions;

public class GestoriaStripeBillingTests
{
    private static readonly GestoriaCompanyBillingLine Co1 =
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alpha SL", "B11111111");
    private static readonly GestoriaCompanyBillingLine Co2 =
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Beta SL", "B22222222");

    [Fact]
    public void ResolveBillableQuantity_MinimumOne()
    {
        Assert.Equal(1, GestoriaStripeBilling.ResolveBillableQuantity([]));
        Assert.Equal(1, GestoriaStripeBilling.ResolveBillableQuantity([Co1]));
        Assert.Equal(2, GestoriaStripeBilling.ResolveBillableQuantity([Co1, Co2]));
    }

    [Fact]
    public void BuildSubscriptionMetadata_IncludesJsonForMultipleCompanies()
    {
        var companyId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var meta = GestoriaStripeBilling.BuildSubscriptionMetadata(companyId, "Gestoría", [Co1, Co2]);

        Assert.Equal("2", meta["gestoriaCompanyCount"]);
        Assert.Contains("gestoriaBreakdownJson", meta.Keys);
        Assert.Contains("Alpha SL", meta["gestoriaBreakdown"]);
        Assert.Equal(companyId.ToString(), meta["companyId"]);
    }

    [Fact]
    public void BuildCheckoutDescription_ShowsPerCompanyLineItems()
    {
        var desc = GestoriaStripeBilling.BuildCheckoutDescription([Co1, Co2, Co2]);
        Assert.Contains("line item por empresa", desc);
    }

    [Fact]
    public void BuildCompanyLineDescription_IncludesNameAndTaxId()
    {
        var desc = GestoriaStripeBilling.BuildCompanyLineDescription(Co1);
        Assert.Contains("Alpha SL", desc);
        Assert.Contains("B11111111", desc);
    }

    [Fact]
    public void BuildCompanyItemMetadata_IncludesCompanyId()
    {
        var meta = GestoriaStripeBilling.BuildCompanyItemMetadata(Co1);
        Assert.Equal(Co1.CompanyId.ToString(), meta[GestoriaStripeBilling.CompanyIdMetadataKey]);
        Assert.Equal("Alpha SL", meta["companyName"]);
    }
}

public class DisabledExpenseAiAssistantTests
{
    [Fact]
    public async Task DisabledAssistant_ReturnsNullAndNotEnabled()
    {
        var ai = new DisabledExpenseAiAssistant();
        Assert.False(ai.IsEnabled);
        Assert.Null(await ai.SuggestAccountCodeAsync("gasolina", null));
        Assert.Null(await ai.SummarizeAnomaliesAsync(1, 0, ["test"]));
        Assert.Null(await ai.SummarizeLiquidityForecastAsync(1000, [(30, 900)]));
    }
}

public class OpenAiCompatibleExpenseAiAssistantTests
{
    [Fact]
    public async Task WhenApiReturns628_SuggestsAccountCode()
    {
        var handler = new FakeOpenAiHandler("628");
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test/v1/") };
        var ai = new OpenAiCompatibleExpenseAiAssistant(
            http,
            Options.Create(new AiOptions { Enabled = true, ApiKey = "test-key", BaseUrl = "https://api.test/v1" }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<OpenAiCompatibleExpenseAiAssistant>.Instance);

        Assert.True(ai.IsEnabled);
        var code = await ai.SuggestAccountCodeAsync("Factura gasolina", "Repsol");
        Assert.Equal("628", code);
    }

    private sealed class FakeOpenAiHandler(string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var json = "{\"choices\":[{\"message\":{\"content\":\"" + content + "\"}}]}";
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}
