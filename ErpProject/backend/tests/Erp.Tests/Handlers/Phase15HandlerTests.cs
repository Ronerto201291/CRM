using Erp.Application.Features.Auth.Commands;
using Erp.Domain.Entities.Core;
using Erp.Domain.Entities.Licensing;
using Erp.Infrastructure.Data;
using Erp.Modules.Billing.Application.Features.Billing.Commands;
using Erp.Modules.Billing.Application.Features.Billing.Queries;
using Erp.Modules.Billing.Application.Features.Quotes;
using Erp.Modules.Billing.Domain.Entities;
using Erp.Modules.Billing.Infrastructure.Data;
using Erp.Modules.Billing.Infrastructure.Services;
using Erp.Modules.Treasury.Application.Features.Treasury.Commands;
using Erp.Modules.Treasury.Application.Features.Treasury.Handlers;
using Erp.Modules.Treasury.Application.Interfaces;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Handlers;

/// <summary>Fase 15: AcceptInvite, PDF invoice/quote, AnulVerifactu, reconcile bank.</summary>
public class Phase15AcceptInviteHandlerTests
{
    private static async Task<(ErpDbContext Ctx, string Token, Guid CompanyId)> SeedPendingInviteAsync()
    {
        var tenant = new FakeTenantContext();
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"phase15-invite-{Guid.NewGuid()}")
            .Options;

        var ctx = new ErpDbContext(options, tenant);
        var invite = await new InviteCompanyHandler(ctx).Handle(new InviteCompanyCommand
        {
            CompanyName = "Invitada SL",
            AdminEmail = $"invite-{Guid.NewGuid():N}@test.local",
        }, CancellationToken.None);

        return (ctx, invite.Token, invite.CompanyId);
    }

    [Fact]
    public async Task AcceptInvite_CreatesAdminUserAndReturnsToken()
    {
        var (ctx, token, companyId) = await SeedPendingInviteAsync();
        await using (ctx)
        {
            var handler = new AcceptInviteHandler(ctx, new Phase15FakeJwtProvider());
            var result = await handler.Handle(new AcceptInviteCommand
            {
                Token = token,
                TaxId = "B87654321",
                Password = "SecurePass1!",
                FirstName = "Carlos",
                LastName = "López",
            }, CancellationToken.None);

            Assert.Equal(companyId, result.CompanyId);
            Assert.Equal("Invitada SL", result.CompanyName);
            Assert.Equal("fake-jwt-token", result.LoginToken);

            var company = await ctx.Companies.IgnoreQueryFilters().SingleAsync(c => c.Id == companyId);
            Assert.Equal("B87654321", company.TaxId);

            var user = await ctx.Users.IgnoreQueryFilters().SingleAsync();
            Assert.Equal("Carlos", user.FirstName);
            Assert.True(BCrypt.Net.BCrypt.Verify("SecurePass1!", user.PasswordHash));

            var invitation = await ctx.TenantInvitations.IgnoreQueryFilters().SingleAsync();
            Assert.True(invitation.IsUsed);
        }
    }

    [Fact]
    public async Task AcceptInvite_Throws_WhenTokenInvalid()
    {
        var tenant = new FakeTenantContext();
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"phase15-invite-bad-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        var handler = new AcceptInviteHandler(ctx, new Phase15FakeJwtProvider());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new AcceptInviteCommand { Token = "invalid-token", TaxId = "B11111111", Password = "x" }, CancellationToken.None));
    }

    [Fact]
    public async Task AcceptInvite_Throws_WhenAlreadyUsed()
    {
        var (ctx, token, _) = await SeedPendingInviteAsync();
        await using (ctx)
        {
            var handler = new AcceptInviteHandler(ctx, new Phase15FakeJwtProvider());
            await handler.Handle(new AcceptInviteCommand
            {
                Token = token,
                TaxId = "B22222222",
                Password = "SecurePass1!",
                FirstName = "Ana",
            }, CancellationToken.None);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.Handle(new AcceptInviteCommand
                {
                    Token = token,
                    TaxId = "B33333333",
                    Password = "SecurePass1!",
                    FirstName = "Otro",
                }, CancellationToken.None));
        }
    }

    [Fact]
    public async Task AcceptInvite_Throws_WhenExpired()
    {
        var (ctx, token, companyId) = await SeedPendingInviteAsync();
        await using (ctx)
        {
            var invitation = await ctx.TenantInvitations.IgnoreQueryFilters().SingleAsync();
            invitation.ExpiresAt = DateTime.UtcNow.AddDays(-1);
            await ctx.SaveChangesAsync();

            var handler = new AcceptInviteHandler(ctx, new Phase15FakeJwtProvider());
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.Handle(new AcceptInviteCommand
                {
                    Token = token,
                    TaxId = "B44444444",
                    Password = "SecurePass1!",
                    FirstName = "Exp",
                }, CancellationToken.None));
        }
    }

    [Fact]
    public async Task AcceptInvite_Throws_WhenTaxIdAlreadyExists()
    {
        var (ctx, token, _) = await SeedPendingInviteAsync();
        await using (ctx)
        {
            ctx.Companies.Add(new Company
            {
                Id = Guid.NewGuid(),
                Name = "Otra empresa",
                TaxId = "B55555555",
                IsActive = true,
                Country = "ES",
            });
            await ctx.SaveChangesAsync();

            var handler = new AcceptInviteHandler(ctx, new Phase15FakeJwtProvider());
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.Handle(new AcceptInviteCommand
                {
                    Token = token,
                    TaxId = "B55555555",
                    Password = "SecurePass1!",
                    FirstName = "Dup",
                }, CancellationToken.None));
        }
    }

    private sealed class Phase15FakeJwtProvider : Erp.Application.Common.Interfaces.IJwtProvider
    {
        public string Generate(User user, Guid? activeCompanyId = null) => "fake-jwt-token";
    }
}

public class Phase15GetInvoicePdfHandlerTests
{
    [Fact]
    public async Task GetInvoicePdf_ReturnsPdf_WhenLocked()
    {
        var companyId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa PDF");

        var billingOptions = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"phase15-pdf-{Guid.NewGuid()}")
            .Options;
        var appOptions = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"phase15-app-pdf-{Guid.NewGuid()}")
            .Options;

        await using var billing = new BillingDbContext(billingOptions, tenant);
        await using var app = new ErpDbContext(appOptions, tenant);

        app.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Empresa PDF",
            TaxId = "B12345674",
            Address = "Calle 1",
            Country = "ES",
            IsActive = true,
        });
        await app.SaveChangesAsync();

        billing.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            CompanyId = companyId,
            Number = "A-2026-000099",
            Series = "A",
            FiscalYear = 2026,
            SequenceNumber = 99,
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(30),
            ClientName = "Cliente PDF",
            ClientNif = "12345678Z",
            Subtotal = 100m,
            TaxAmount = 21m,
            Total = 121m,
            Status = "Locked",
            IsLocked = true,
            Hash = "abc123",
            InvoiceLines =
            [
                new InvoiceLine
                {
                    Id = Guid.NewGuid(),
                    Description = "Servicio",
                    Quantity = 1,
                    UnitPrice = 100m,
                    TaxRate = 21m,
                    TaxAmount = 21m,
                    LineTotal = 100m,
                },
            ],
        });
        await billing.SaveChangesAsync();

        var handler = new GetInvoicePdfHandler(
            billing, app, new FakeClientInfoService(), new FakeInvoicePdfService(), new FakeFileStorageService());

        var result = await handler.Handle(new GetInvoicePdfQuery(invoiceId), CancellationToken.None);

        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(result.PdfBytes));
        Assert.Equal("A-2026-000099", result.InvoiceNumber);
        Assert.Contains("Factura_A-2026-000099", result.FileName);
    }

    [Fact]
    public async Task GetInvoicePdf_Throws_WhenNotLocked()
    {
        var companyId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var billingOptions = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"phase15-pdf-draft-{Guid.NewGuid()}")
            .Options;
        var appOptions = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"phase15-app-draft-{Guid.NewGuid()}")
            .Options;

        await using var billing = new BillingDbContext(billingOptions, tenant);
        await using var app = new ErpDbContext(appOptions, tenant);
        billing.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            CompanyId = companyId,
            Number = "A-2026-000001",
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(30),
            IsLocked = false,
            Total = 100m,
        });
        await billing.SaveChangesAsync();

        var handler = new GetInvoicePdfHandler(
            billing, app, new FakeClientInfoService(), new FakeInvoicePdfService(), new FakeFileStorageService());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new GetInvoicePdfQuery(invoiceId), CancellationToken.None));
    }

    [Fact]
    public async Task GetInvoicePdf_Throws_WhenNotFound()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");

        var billingOptions = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"phase15-pdf-miss-{Guid.NewGuid()}")
            .Options;
        var appOptions = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"phase15-app-miss-{Guid.NewGuid()}")
            .Options;

        await using var billing = new BillingDbContext(billingOptions, tenant);
        await using var app = new ErpDbContext(appOptions, tenant);

        var handler = new GetInvoicePdfHandler(
            billing, app, new FakeClientInfoService(), new FakeInvoicePdfService(), new FakeFileStorageService());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new GetInvoicePdfQuery(Guid.NewGuid()), CancellationToken.None));
    }
}

public class Phase15GetQuotePdfHandlerTests
{
    [Fact]
    public async Task GetQuotePdf_ReturnsPdf_WhenQuoteExists()
    {
        var companyId = Guid.NewGuid();
        var quoteId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa quote");

        var billingOptions = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"phase15-quote-pdf-{Guid.NewGuid()}")
            .Options;
        var appOptions = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"phase15-app-quote-{Guid.NewGuid()}")
            .Options;

        await using var billing = new BillingDbContext(billingOptions, tenant);
        await using var app = new ErpDbContext(appOptions, tenant);
        app.Companies.Add(new Company { Id = companyId, Name = "Empresa quote", TaxId = "B12345674", Country = "ES", IsActive = true });
        await app.SaveChangesAsync();

        billing.Quotes.Add(new Quote
        {
            Id = quoteId,
            CompanyId = companyId,
            Number = "PRE-2026-00001",
            SeriesPrefix = "PRE",
            FiscalYear = 2026,
            IssueDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(30),
            ClientName = "Prospecto",
            SubtotalBeforeDisc = 500m,
            TaxBaseAmount = 500m,
            TaxAmount = 105m,
            TotalAmount = 605m,
            TaxBreakdown = """[{"rate":21,"base":500,"tax":105}]""",
            Lines =
            [
                new QuoteLine
                {
                    Id = Guid.NewGuid(),
                    SortOrder = 1,
                    Description = "Consultoría",
                    Quantity = 1,
                    UnitPrice = 500m,
                    LineTaxBase = 500m,
                    TaxRate = 21m,
                    LineTaxAmount = 105m,
                    LineTotalAmount = 605m,
                },
            ],
        });
        await billing.SaveChangesAsync();

        var handler = new GetQuotePdfHandler(billing, app, new FakeQuotePdfService());
        var result = await handler.Handle(new GetQuotePdfQuery { Id = quoteId }, CancellationToken.None);

        Assert.Equal(4, result.PdfBytes.Length);
        Assert.Equal("PRE-2026-00001", result.QuoteNumber);
        Assert.Contains("Presupuesto_PRE-2026-00001", result.FileName);
    }

    [Fact]
    public async Task GetQuotePdf_Throws_WhenNotFound()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");

        var billingOptions = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"phase15-quote-miss-{Guid.NewGuid()}")
            .Options;
        var appOptions = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"phase15-app-quote-miss-{Guid.NewGuid()}")
            .Options;

        await using var billing = new BillingDbContext(billingOptions, tenant);
        await using var app = new ErpDbContext(appOptions, tenant);

        var handler = new GetQuotePdfHandler(billing, app, new FakeQuotePdfService());
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new GetQuotePdfQuery { Id = Guid.NewGuid() }, CancellationToken.None));
    }
}

public class Phase15AnulVerifactuHandlerTests
{
    [Fact]
    public async Task AnulVerifactu_EnqueuesAnulacion_WhenHuellaPresent()
    {
        var companyId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa VF");

        await using var billing = CreateBillingContext(tenant);
        await using var app = CreateAppContext(tenant, companyId);
        billing.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            CompanyId = companyId,
            Number = "A-2026-000050",
            Series = "A",
            FiscalYear = 2026,
            SequenceNumber = 50,
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(30),
            VerifactuHuella = "huella-test",
            VerifactuSubmittedAt = DateTime.UtcNow,
            Total = 121m,
            TaxAmount = 21m,
            Subtotal = 100m,
        });
        await billing.SaveChangesAsync();

        var gateway = new FakeVerifactuSubmissionGateway();
        var registrar = new VerifactuAnulacionRegistrar(
            billing, app, new FakeVerifactuService(), gateway,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<VerifactuAnulacionRegistrar>.Instance);
        var handler = new AnulVerifactuInvoiceHandler(registrar);
        var ok = await handler.Handle(new AnulVerifactuInvoiceCommand { InvoiceId = invoiceId }, CancellationToken.None);

        Assert.True(ok);
        Assert.Contains(invoiceId, gateway.EnqueuedAnulaciones);
    }

    [Fact]
    public async Task AnulVerifactu_ReturnsFalse_WhenInvoiceMissing()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");

        await using var billing = CreateBillingContext(tenant);
        await using var app = CreateAppContext(tenant, Guid.NewGuid());

        var registrar = new VerifactuAnulacionRegistrar(
            billing, app, new FakeVerifactuService(), new FakeVerifactuSubmissionGateway(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<VerifactuAnulacionRegistrar>.Instance);
        var handler = new AnulVerifactuInvoiceHandler(registrar);
        var ok = await handler.Handle(new AnulVerifactuInvoiceCommand { InvoiceId = Guid.NewGuid() }, CancellationToken.None);
        Assert.False(ok);
    }

    [Fact]
    public async Task AnulVerifactu_Throws_WhenNoHuella()
    {
        var companyId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa VF");

        await using var billing = CreateBillingContext(tenant);
        await using var app = CreateAppContext(tenant, companyId);
        billing.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            CompanyId = companyId,
            Number = "A-2026-000051",
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(30),
            VerifactuSubmittedAt = DateTime.UtcNow,
            Total = 100m,
        });
        await billing.SaveChangesAsync();

        var registrar = new VerifactuAnulacionRegistrar(
            billing, app, new FakeVerifactuService(), new FakeVerifactuSubmissionGateway(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<VerifactuAnulacionRegistrar>.Instance);
        var handler = new AnulVerifactuInvoiceHandler(registrar);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new AnulVerifactuInvoiceCommand { InvoiceId = invoiceId }, CancellationToken.None));
    }

    private static BillingDbContext CreateBillingContext(FakeTenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"phase15-vf-{Guid.NewGuid()}")
            .Options;
        return new BillingDbContext(options, tenant);
    }

    private static ErpDbContext CreateAppContext(FakeTenantContext tenant, Guid companyId)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"phase15-vf-app-{Guid.NewGuid()}")
            .Options;
        var ctx = new ErpDbContext(options, tenant);
        ctx.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Empresa VF",
            TaxId = "B12345674",
            Address = "Calle 1",
            SubscriptionId = Guid.NewGuid(),
        });
        ctx.SaveChanges();
        return ctx;
    }
}

public class Phase15ReconcileBankAccountHandlerTests
{
    [Fact]
    public async Task ReconcileBankAccount_ReturnsMatchedSummary()
    {
        var handler = new ReconcileBankAccountHandler(new FakeBankReconciliationService(3, 1500.50m));
        var result = await handler.Handle(new ReconcileBankAccountCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(3, result.MatchedCount);
        Assert.Equal(1500.50m, result.MatchedAmount);
        Assert.Contains("Conciliados 3", result.Message);
    }

    [Fact]
    public async Task ReconcileBankAccount_ReturnsNoMatchesMessage()
    {
        var handler = new ReconcileBankAccountHandler(new FakeBankReconciliationService(0, 0m));
        var result = await handler.Handle(new ReconcileBankAccountCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(0, result.MatchedCount);
        Assert.Equal("No se encontraron coincidencias automáticas", result.Message);
    }

    private sealed class FakeBankReconciliationService(int count, decimal amount) : IBankReconciliationService
    {
        public Task<ReconciliationResult> ReconcileAsync(Guid bankAccountId, CancellationToken ct = default)
            => Task.FromResult(new ReconciliationResult { MatchedCount = count, MatchedAmount = amount });
    }
}
