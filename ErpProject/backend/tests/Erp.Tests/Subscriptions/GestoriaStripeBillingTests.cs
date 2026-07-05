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

    // La ruta multi-empresa (MaxCompanies>1) de StripeService.SyncGestoriaSubscriptionQuantityAsync
    // no tenía ningún test — contra-auditoría jul 2026. La decisión de qué crear/actualizar/
    // borrar se extrajo a PlanSubscriptionItemSync (función pura, sin llamadas a Stripe)
    // precisamente para poder cubrirla aquí sin necesitar un fake del SDK de Stripe.

    [Fact]
    public void PlanSubscriptionItemSync_NewCompany_PlansCreate()
    {
        var plan = GestoriaStripeBilling.PlanSubscriptionItemSync(
            existingByCompany: new Dictionary<Guid, ExistingSubscriptionItem>(),
            targetCompanies: [Co1]);

        var action = Assert.Single(plan);
        Assert.Equal(SubscriptionItemSyncKind.Create, action.Kind);
        Assert.Equal(Co1.CompanyId, action.CompanyId);
        Assert.Null(action.ItemId);
    }

    [Fact]
    public void PlanSubscriptionItemSync_ExistingWithWrongQuantity_PlansUpdate()
    {
        var existing = new Dictionary<Guid, ExistingSubscriptionItem>
        {
            [Co1.CompanyId] = new ExistingSubscriptionItem("si_123", Quantity: 2),
        };

        var plan = GestoriaStripeBilling.PlanSubscriptionItemSync(existing, [Co1]);

        var action = Assert.Single(plan);
        Assert.Equal(SubscriptionItemSyncKind.UpdateQuantity, action.Kind);
        Assert.Equal("si_123", action.ItemId);
    }

    [Fact]
    public void PlanSubscriptionItemSync_ExistingWithCorrectQuantity_PlansNothing()
    {
        var existing = new Dictionary<Guid, ExistingSubscriptionItem>
        {
            [Co1.CompanyId] = new ExistingSubscriptionItem("si_123", Quantity: 1),
        };

        var plan = GestoriaStripeBilling.PlanSubscriptionItemSync(existing, [Co1]);

        Assert.Empty(plan);
    }

    [Fact]
    public void PlanSubscriptionItemSync_CompanyRemovedFromGestoria_PlansDelete()
    {
        var existing = new Dictionary<Guid, ExistingSubscriptionItem>
        {
            [Co1.CompanyId] = new ExistingSubscriptionItem("si_keep", Quantity: 1),
            [Co2.CompanyId] = new ExistingSubscriptionItem("si_orphan", Quantity: 1),
        };

        // Co2 ya no forma parte de la gestoría — solo se mantiene Co1.
        var plan = GestoriaStripeBilling.PlanSubscriptionItemSync(existing, [Co1]);

        var action = Assert.Single(plan);
        Assert.Equal(SubscriptionItemSyncKind.Delete, action.Kind);
        Assert.Equal("si_orphan", action.ItemId);
        Assert.Equal(Co2.CompanyId, action.CompanyId);
    }

    [Fact]
    public void PlanSubscriptionItemSync_MultiCompanyRealisticScenario_PlansAllThreeKinds()
    {
        var co3 = new GestoriaCompanyBillingLine(
            Guid.Parse("33333333-3333-3333-3333-333333333333"), "Gamma SL", "B33333333");

        // Co1 ya tiene item con cantidad correcta (nada que hacer), Co2 tiene cantidad
        // desincronizada (2 -> debe pasar a 1), Co3 es nueva (crear), y hay un item huérfano
        // de una empresa que ya no pertenece a la gestoría (borrar).
        var orphanCompanyId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var existing = new Dictionary<Guid, ExistingSubscriptionItem>
        {
            [Co1.CompanyId] = new ExistingSubscriptionItem("si_co1", Quantity: 1),
            [Co2.CompanyId] = new ExistingSubscriptionItem("si_co2", Quantity: 2),
            [orphanCompanyId] = new ExistingSubscriptionItem("si_orphan", Quantity: 1),
        };

        var plan = GestoriaStripeBilling.PlanSubscriptionItemSync(existing, [Co1, Co2, co3]);

        Assert.Equal(3, plan.Count);
        Assert.Contains(plan, a => a.Kind == SubscriptionItemSyncKind.Create && a.CompanyId == co3.CompanyId);
        Assert.Contains(plan, a => a.Kind == SubscriptionItemSyncKind.UpdateQuantity && a.ItemId == "si_co2");
        Assert.Contains(plan, a => a.Kind == SubscriptionItemSyncKind.Delete && a.ItemId == "si_orphan");
        Assert.DoesNotContain(plan, a => a.CompanyId == Co1.CompanyId);
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
