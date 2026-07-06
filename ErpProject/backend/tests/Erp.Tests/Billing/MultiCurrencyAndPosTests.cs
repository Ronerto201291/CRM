using Erp.Application.Common.Events;
using Erp.Modules.Treasury.Application.Features.Pos;
using Erp.Modules.Treasury.Domain.Entities;
using Erp.Modules.Treasury.Infrastructure.Data;
using Erp.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Billing;

public class RegisterPosPaymentHandlerTests
{
    [Fact]
    public async Task Handle_PersistsPaymentAndPublishesEvent()
    {
        var companyId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa");

        var options = new DbContextOptionsBuilder<TreasuryDbContext>()
            .UseInMemoryDatabase($"pos-{Guid.NewGuid()}")
            .Options;
        await using var ctx = new TreasuryDbContext(options, tenant);
        ctx.PosTerminals.Add(new PosTerminal
        {
            Id = terminalId,
            CompanyId = companyId,
            Name = "TPV Mostrador",
            TerminalCode = "TPV-01",
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var publisher = new FakePublisher();
        var handler = new RegisterPosPaymentHandler(ctx, tenant, publisher);
        var result = await handler.Handle(new RegisterPosPaymentCommand(terminalId, invoiceId, 50m, "ref-1"), CancellationToken.None);

        Assert.Equal(invoiceId, result.InvoiceId);
        var evt = Assert.IsType<InvoiceCardPaymentRequestedEvent>(Assert.Single(publisher.Published));
        Assert.Equal(invoiceId, evt.InvoiceId);
    }
}
