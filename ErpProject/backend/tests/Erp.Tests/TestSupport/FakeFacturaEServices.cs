using Erp.Application.Common.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using System.Text;

namespace Erp.Tests.TestSupport;

public sealed class FakeFacturaEService : IFacturaEService
{
    public byte[] XmlBytes { get; set; } = Encoding.UTF8.GetBytes(
        """<?xml version="1.0" encoding="UTF-8"?><Facturae xmlns="http://www.facturae.gob.es/formato/Versiones/Facturaev3_2_2.xml"></Facturae>""");
    public string FileName { get; set; } = "factura-test.xml";

    public Task<(byte[] XmlBytes, string FileName)> GenerateAsync(Guid invoiceId, Guid tenantId, CancellationToken ct = default)
        => Task.FromResult((XmlBytes, FileName));

    public Task<(byte[] XmlBytes, string FileName)> GenerateSignedAsync(Guid invoiceId, Guid tenantId, CancellationToken ct = default)
        => Task.FromResult((XmlBytes, FileName.Replace(".xml", "-signed.xml")));
}

public sealed class FakeFaceSubmissionService : IFaceSubmissionService
{
    public bool IsConfigured { get; set; } = true;
    public FaceSubmissionResult Result { get; set; } = new(true, "Enviado a FACe (stub)", "FACE-REF-001", 200, "OK");

    public Task<FaceSubmissionResult> SubmitAsync(byte[] signedXml, string fileName, CancellationToken ct = default)
        => Task.FromResult(Result);
}

public sealed class FakeSubscriptionBillingService : ISubscriptionBillingService
{
    public string CheckoutUrl { get; set; } = "https://checkout.stripe.com/test-session";
    public string PortalUrl { get; set; } = "https://billing.stripe.com/test-portal";
    public IReadOnlyList<SubscriptionInvoiceDto> Invoices { get; set; } =
    [
        new("inv_1", DateTime.UtcNow.AddDays(-30), 49m, "eur", "paid", null, "Plan Pro"),
    ];

    public Task<string> CreateCheckoutSessionAsync(
        Guid companyId, string planName, string successUrl, string cancelUrl, CancellationToken ct = default)
        => Task.FromResult(CheckoutUrl);

    public Task<string> CreatePortalSessionAsync(Guid companyId, string returnUrl, CancellationToken ct = default)
        => Task.FromResult(PortalUrl);

    public Task<IReadOnlyList<SubscriptionInvoiceDto>> ListInvoicesAsync(Guid companyId, CancellationToken ct = default)
        => Task.FromResult(Invoices);
}

public sealed class FakePermissionService : IPermissionService
{
    public IReadOnlyList<string> Permissions { get; set; } = ["Billing:Read", "Crm:Write"];

    public Task<bool> HasPermissionAsync(string resource, string action, CancellationToken ct = default)
        => Task.FromResult(Permissions.Contains($"{resource}:{action}"));

    public Task<bool> UserHasPermissionAsync(Guid userId, string resource, string action, CancellationToken ct = default)
        => HasPermissionAsync(resource, action, ct);

    public Task<IReadOnlyList<string>> GetUserPermissionsAsync(CancellationToken ct = default)
        => Task.FromResult(Permissions);
}
