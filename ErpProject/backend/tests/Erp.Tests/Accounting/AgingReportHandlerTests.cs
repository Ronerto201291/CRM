using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Features.Aging;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Tests.TestSupport;
using Xunit;

namespace Erp.Tests.Accounting;

public class GetAgingReportHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsMappedReport()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var reader = new FakeAgingReportReader();
        var handler = new GetAgingReportHandler(reader, tenant);

        var result = await handler.Handle(new GetAgingReportQuery(), CancellationToken.None);

        Assert.Equal(1000m, result.Receivables.TotalAmount);
        Assert.Equal(500m, result.Payables.TotalAmount);
        Assert.Equal(30m, result.DSO);
    }

    [Fact]
    public async Task Handle_ThrowsWithoutTenant()
    {
        var tenant = new FakeTenantContext();
        var handler = new GetAgingReportHandler(new FakeAgingReportReader(), tenant);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new GetAgingReportQuery(), CancellationToken.None));
    }

    private sealed class FakeAgingReportReader : IAgingReportReader
    {
        public Task<AgingReportData> GetReportAsync(Guid companyId, DateTime? asOf, CancellationToken ct = default)
        {
            var line = new AgingLineRow(
                Guid.NewGuid(), "FAC-001", "Cliente SL",
                DateTime.UtcNow.AddDays(-10), 10, 10, 1000m, "Open");

            return Task.FromResult(new AgingReportData(
                DateTime.UtcNow,
                new AgingBucketRow("Receivables", 1000m, 800m, 100m, 50m, 50m, [line]),
                new AgingBucketRow("Payables", 500m, 400m, 50m, 25m, 25m, []),
                30m, 45m, "Datos de prueba"));
        }
    }
}
