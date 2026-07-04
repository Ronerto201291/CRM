using Erp.Application.Common;
using Erp.Application.Common.Interfaces;
using Erp.Application.DTOs;
using Erp.Application.Features.Admin.Queries;
using Erp.Application.Features.Auth.Commands;
using Erp.Application.Features.Auth.Handlers;
using Erp.Application.Features.Auth.Queries;
using Erp.Application.Features.FiscalCalendar;
using Erp.Application.Features.PublicApi;
using Erp.Application.Features.Users.Commands;
using Erp.Application.Features.Users.Handlers;
using Erp.Application.Features.Users.Queries;
using Erp.Domain.Entities.Core;
using Erp.Domain.Entities.Licensing;
using Erp.Infrastructure.Data;
using Erp.Modules.Billing.Application.Features.Billing.Handlers;
using Erp.Modules.Billing.Application.Features.Billing.Queries;
using Erp.Modules.Billing.Domain.Entities;
using Erp.Modules.Billing.Infrastructure.Data;
using Erp.Modules.Treasury.Application.Features.Treasury.Commands;
using Erp.Modules.Treasury.Application.Features.Treasury.Handlers;
using Erp.Modules.Treasury.Domain.Entities;
using Erp.Modules.Treasury.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Erp.Tests.Handlers;

/// <summary>Fase 14: auth login/invite, users, admin, billing público, treasury execute, fiscal calendar restante.</summary>
public class Phase14LoginHandlerTests
{
    private static ErpDbContext CreateContext(Guid companyId, out FakeTenantContext tenant)
    {
        tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"phase14-login-{Guid.NewGuid()}")
            .Options;
        return new ErpDbContext(options, tenant);
    }

    [Fact]
    public async Task Login_ReturnsToken_WhenCredentialsValid()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var ctx = CreateContext(companyId, out _);
        ctx.Companies.Add(new Company { Id = companyId, Name = "Login Co", TaxId = "B12345674", IsActive = true, Country = "ES" });
        ctx.Users.Add(new User
        {
            Id = userId,
            CompanyId = companyId,
            Email = "login@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("SecurePass1!"),
            FirstName = "Ana",
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var getCompanies = new GetUserCompaniesHandler(ctx);
        var mediator = new ConfigurableFakeMediator();
        mediator.Register<GetUserCompaniesQuery, IReadOnlyList<CompanyMembershipDto>>(q =>
            getCompanies.Handle(q, CancellationToken.None).GetAwaiter().GetResult());

        var handler = new LoginCommandHandler(ctx, new Phase14FakeJwtProvider(), mediator);
        var result = await handler.Handle(new LoginCommand { Email = "login@test.com", Password = "SecurePass1!" }, CancellationToken.None);

        Assert.False(result.RequiresTwoFactor);
        Assert.Equal("fake-jwt-token", result.Token);
        Assert.Equal(companyId.ToString(), result.CompanyId);
        Assert.Equal("Login Co", result.CompanyName);
    }

    [Fact]
    public async Task Login_Throws_WhenPasswordInvalid()
    {
        var companyId = Guid.NewGuid();
        await using var ctx = CreateContext(companyId, out _);
        ctx.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Email = "bad@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("correct"),
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var handler = new LoginCommandHandler(ctx, new Phase14FakeJwtProvider(), new FakeMediator());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new LoginCommand { Email = "bad@test.com", Password = "wrong" }, CancellationToken.None));
    }

    [Fact]
    public async Task Login_ReturnsRequiresTwoFactor_WhenEnabled()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var ctx = CreateContext(companyId, out _);
        ctx.Users.Add(new User
        {
            Id = userId,
            CompanyId = companyId,
            Email = "2fa@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("SecurePass1!"),
            IsActive = true,
            TwoFactorEnabled = true,
        });
        await ctx.SaveChangesAsync();

        var handler = new LoginCommandHandler(ctx, new Phase14FakeJwtProvider(), new FakeMediator());
        var result = await handler.Handle(new LoginCommand { Email = "2fa@test.com", Password = "SecurePass1!" }, CancellationToken.None);

        Assert.True(result.RequiresTwoFactor);
        Assert.True(string.IsNullOrEmpty(result.Token));
        Assert.Equal(userId, result.UserId);
    }

    private sealed class Phase14FakeJwtProvider : IJwtProvider
    {
        public string Generate(User user, Guid? activeCompanyId = null) => "fake-jwt-token";
    }
}

public class Phase14InviteCompanyHandlerTests
{
    [Fact]
    public async Task InviteCompany_CreatesTrialCompanyAndInvitation()
    {
        var tenant = new FakeTenantContext();
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"phase14-invite-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        var handler = new InviteCompanyHandler(ctx);
        var result = await handler.Handle(new InviteCompanyCommand
        {
            CompanyName = "Nueva Empresa SL",
            AdminEmail = "admin@nueva.test",
        }, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.CompanyId);
        Assert.False(string.IsNullOrEmpty(result.Token));
        Assert.Contains("/register?token=", result.InviteUrlPath);

        var company = await ctx.Companies.IgnoreQueryFilters().SingleAsync();
        Assert.StartsWith("PENDING-", company.TaxId);
        tenant.SetTenant(result.CompanyId, company.Name);
        Assert.Equal(3, await ctx.Roles.CountAsync());
        Assert.Single(await ctx.TenantInvitations.ToListAsync());
    }

    [Fact]
    public async Task InviteCompany_Throws_WhenEmailAlreadyRegistered()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"phase14-invite-dup-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Email = "dup@test.com",
            PasswordHash = "hash",
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var handler = new InviteCompanyHandler(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new InviteCompanyCommand { CompanyName = "X", AdminEmail = "dup@test.com" }, CancellationToken.None));
    }
}

public class Phase14UserHandlerTests
{
    private static (ErpDbContext Ctx, FakeTenantContext Tenant, Guid CompanyId) CreateTenantContext()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"phase14-users-{Guid.NewGuid()}")
            .Options;
        return (new ErpDbContext(options, tenant), tenant, companyId);
    }

    [Fact]
    public async Task GetUsers_ReturnsTenantUsersOrderedByName()
    {
        var (ctx, tenant, companyId) = CreateTenantContext();
        await using (ctx)
        {
            ctx.Users.AddRange(
                new User { Id = Guid.NewGuid(), CompanyId = companyId, Email = "z@test.com", FirstName = "Zoe", IsActive = true },
                new User { Id = Guid.NewGuid(), CompanyId = companyId, Email = "a@test.com", FirstName = "Ana", IsActive = true });
            await ctx.SaveChangesAsync();

            var handler = new GetUsersHandler(ctx, tenant);
            var users = await handler.Handle(new GetUsersQuery(), CancellationToken.None);

            Assert.Equal(2, users.Count);
            Assert.Equal("Ana", users[0].FirstName);
        }
    }

    [Fact]
    public async Task GetRoles_ReturnsTenantRoles()
    {
        var (ctx, tenant, companyId) = CreateTenantContext();
        await using (ctx)
        {
            var roleId = Guid.NewGuid();
            ctx.Roles.Add(new Role { Id = roleId, CompanyId = companyId, Name = "Contable" });
            await ctx.SaveChangesAsync();

            var handler = new GetRolesHandler(ctx, tenant);
            var roles = await handler.Handle(new GetRolesQuery(), CancellationToken.None);

            Assert.Single(roles);
            Assert.Equal("Contable", roles[0].Name);
        }
    }

    [Fact]
    public async Task CreateUser_ReturnsTempPasswordAndLinksUserCompany()
    {
        var (ctx, tenant, companyId) = CreateTenantContext();
        await using (ctx)
        {
            var roleId = Guid.NewGuid();
            ctx.Roles.Add(new Role { Id = roleId, CompanyId = companyId, Name = "Viewer" });
            await ctx.SaveChangesAsync();

            var handler = new CreateUserHandler(ctx, tenant);
            var result = await handler.Handle(new CreateUserCommand
            {
                Email = "nuevo@test.com",
                FirstName = "Nuevo",
                LastName = "Usuario",
                RoleId = roleId,
            }, CancellationToken.None);

            Assert.False(string.IsNullOrEmpty(result.TempPassword));
            Assert.Equal("nuevo@test.com", result.Email);
            Assert.Single(await ctx.UserCompanies.ToListAsync());
        }
    }

    [Fact]
    public async Task CreateUser_Throws_WhenEmailDuplicate()
    {
        var (ctx, tenant, companyId) = CreateTenantContext();
        await using (ctx)
        {
            ctx.Users.Add(new User { Id = Guid.NewGuid(), CompanyId = companyId, Email = "dup@test.com", IsActive = true });
            await ctx.SaveChangesAsync();

            var handler = new CreateUserHandler(ctx, tenant);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.Handle(new CreateUserCommand { Email = "dup@test.com", FirstName = "X" }, CancellationToken.None));
        }
    }

    [Fact]
    public async Task UpdateUserRole_AssignsRole()
    {
        var (ctx, tenant, companyId) = CreateTenantContext();
        await using (ctx)
        {
            var userId = Guid.NewGuid();
            var roleId = Guid.NewGuid();
            ctx.Roles.Add(new Role { Id = roleId, CompanyId = companyId, Name = "Manager" });
            ctx.Users.Add(new User { Id = userId, CompanyId = companyId, Email = "u@test.com", IsActive = true });
            await ctx.SaveChangesAsync();

            var handler = new UpdateUserRoleHandler(ctx, tenant);
            var ok = await handler.Handle(new UpdateUserRoleCommand { UserId = userId, RoleId = roleId }, CancellationToken.None);

            Assert.True(ok);
            Assert.Equal(roleId, (await ctx.Users.SingleAsync()).RoleId);
        }
    }

    [Fact]
    public async Task UpdateUserStatus_TogglesActive()
    {
        var (ctx, tenant, companyId) = CreateTenantContext();
        await using (ctx)
        {
            var userId = Guid.NewGuid();
            ctx.Users.Add(new User { Id = userId, CompanyId = companyId, Email = "u@test.com", IsActive = true });
            await ctx.SaveChangesAsync();

            var handler = new UpdateUserStatusHandler(ctx, tenant);
            var ok = await handler.Handle(new UpdateUserStatusCommand { UserId = userId, IsActive = false }, CancellationToken.None);

            Assert.True(ok);
            Assert.False((await ctx.Users.SingleAsync()).IsActive);
        }
    }
}

public class Phase14AdminHandlerTests
{
    [Fact]
    public async Task GetAdminCompanies_ReturnsPlanInfo()
    {
        var tenant = new FakeTenantContext();
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"phase14-admin-co-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        var subId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        tenant.SetTenant(companyId, "Admin Co");
        ctx.Subscriptions.Add(new Subscription { Id = subId, PlanName = "Pro", IsActive = true, CompanyId = companyId });
        ctx.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Admin Co",
            TaxId = "B12345674",
            IsActive = true,
            Country = "ES",
            SubscriptionId = subId,
        });
        await ctx.SaveChangesAsync();

        var handler = new GetAdminCompaniesHandler(ctx);
        var companies = await handler.Handle(new GetAdminCompaniesQuery(), CancellationToken.None);

        Assert.Single(companies);
        Assert.Equal("Pro", companies[0].PlanName);
        Assert.True(companies[0].SubscriptionActive);
    }

    [Fact]
    public async Task GetAdminInvitations_ReturnsPendingInvites()
    {
        var tenant = new FakeTenantContext();
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"phase14-admin-inv-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        var companyId = Guid.NewGuid();
        ctx.Companies.Add(new Company { Id = companyId, Name = "Inv Co", TaxId = "B99999999", IsActive = true, Country = "ES" });
        ctx.TenantInvitations.Add(new TenantInvitation
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Email = "invite@test.com",
            Token = "tok",
            ExpiresAt = DateTime.UtcNow.AddDays(3),
            IsUsed = false,
        });
        await ctx.SaveChangesAsync();

        var handler = new GetAdminInvitationsHandler(ctx);
        var invites = await handler.Handle(new GetAdminInvitationsQuery(), CancellationToken.None);

        Assert.Single(invites);
        Assert.Equal("invite@test.com", invites[0].Email);
        Assert.Equal("Inv Co", invites[0].CompanyName);
    }
}

public class Phase14BillingPublicHandlerTests
{
    private static BillingDbContext CreateBilling(Guid companyId)
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"phase14-billing-pub-{Guid.NewGuid()}")
            .Options;
        return new BillingDbContext(options, tenant);
    }

    [Fact]
    public async Task GetPublicInvoices_FiltersByYearAndStatus()
    {
        var companyId = Guid.NewGuid();
        await using var ctx = CreateBilling(companyId);
        ctx.Invoices.AddRange(
            new Invoice
            {
                Id = Guid.NewGuid(), CompanyId = companyId, Number = "A-2026-000001", Series = "A",
                FiscalYear = 2026, SequenceNumber = 1, IssueDate = DateTime.UtcNow, DueDate = DateTime.UtcNow.AddDays(30),
                ClientName = "C1", Subtotal = 100, TaxAmount = 21, Total = 121, Status = "Locked", IsLocked = true,
            },
            new Invoice
            {
                Id = Guid.NewGuid(), CompanyId = companyId, Number = "A-2025-000001", Series = "A",
                FiscalYear = 2025, SequenceNumber = 1, IssueDate = DateTime.UtcNow, DueDate = DateTime.UtcNow.AddDays(30),
                ClientName = "C2", Subtotal = 50, TaxAmount = 10.5m, Total = 60.5m, Status = "Paid", IsLocked = true,
            });
        await ctx.SaveChangesAsync();

        var handler = new GetPublicInvoicesHandler(ctx);
        var invoices = await handler.Handle(new GetPublicInvoicesQuery { Year = 2026, Status = "Locked" }, CancellationToken.None);

        Assert.Single(invoices);
        Assert.Equal("A-2026-000001", invoices[0].Number);
    }

    [Fact]
    public async Task GetPublicInvoiceById_ReturnsDetailWithLines()
    {
        var companyId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        await using var ctx = CreateBilling(companyId);
        ctx.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            CompanyId = companyId,
            Number = "A-2026-000002",
            Series = "A",
            FiscalYear = 2026,
            SequenceNumber = 2,
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(30),
            ClientName = "Cliente",
            Subtotal = 200,
            TaxAmount = 42,
            Total = 242,
            Status = "Locked",
            IsLocked = true,
            InvoiceLines =
            [
                new InvoiceLine
                {
                    Id = Guid.NewGuid(),
                    InvoiceId = invoiceId,
                    Description = "Servicio",
                    Quantity = 2,
                    UnitPrice = 100,
                    TaxRate = 21,
                    LineTotal = 242,
                },
            ],
        });
        await ctx.SaveChangesAsync();

        var handler = new GetPublicInvoiceByIdHandler(ctx);
        var detail = await handler.Handle(new GetPublicInvoiceByIdQuery { Id = invoiceId }, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Single(detail!.Lines);
        Assert.Equal("Servicio", detail.Lines[0].Description);
    }

    [Fact]
    public async Task GetPublicInvoiceById_ReturnsNull_WhenMissing()
    {
        var companyId = Guid.NewGuid();
        await using var ctx = CreateBilling(companyId);
        var handler = new GetPublicInvoiceByIdHandler(ctx);
        var detail = await handler.Handle(new GetPublicInvoiceByIdQuery { Id = Guid.NewGuid() }, CancellationToken.None);
        Assert.Null(detail);
    }
}

public class Phase14TreasuryExecuteHandlerTests
{
    [Fact]
    public async Task ExecutePaymentOrder_CreatesBankMovement()
    {
        var companyId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");
        var options = new DbContextOptionsBuilder<TreasuryDbContext>()
            .UseInMemoryDatabase($"phase14-treasury-exec-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new TreasuryDbContext(options, tenant);
        ctx.PaymentOrders.Add(new PaymentOrder
        {
            Id = orderId,
            CompanyId = companyId,
            PaymentType = "Supplier",
            BeneficiaryName = "Proveedor",
            BeneficiaryIban = "ES9121000418450200051332",
            Description = "Pago factura",
            Amount = 500m,
            Status = "Approved",
            BankAccountId = bankId,
        });
        await ctx.SaveChangesAsync();

        var handler = new ExecutePaymentOrderHandler(ctx, tenant);
        var result = await handler.Handle(new ExecutePaymentOrderCommand(orderId), CancellationToken.None);

        Assert.Equal("Executed", result.Status);
        Assert.NotNull(result.ExecutedAt);
        var movement = await ctx.BankMovements.SingleAsync();
        Assert.Equal(-500m, movement.Amount);
        Assert.Equal(bankId, movement.BankAccountId);
    }

    [Fact]
    public async Task ExecutePaymentOrder_Throws_WhenAlreadyExecuted()
    {
        var companyId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");
        var options = new DbContextOptionsBuilder<TreasuryDbContext>()
            .UseInMemoryDatabase($"phase14-treasury-exec-bad-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new TreasuryDbContext(options, tenant);
        ctx.PaymentOrders.Add(new PaymentOrder
        {
            Id = orderId,
            CompanyId = companyId,
            BeneficiaryName = "X",
            Amount = 100m,
            Status = "Executed",
        });
        await ctx.SaveChangesAsync();

        var handler = new ExecutePaymentOrderHandler(ctx, tenant);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new ExecutePaymentOrderCommand(orderId), CancellationToken.None));
    }
}

public class Phase14FiscalCalendarHandlerTests
{
    private static (ErpDbContext Ctx, FakeTenantContext Tenant, Guid CompanyId) CreateTenant()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"phase14-fiscal-{Guid.NewGuid()}")
            .Options;
        return (new ErpDbContext(options, tenant), tenant, companyId);
    }

    [Fact]
    public async Task GenerateFiscalCalendar_AddsNewEventsAndSkipsDuplicates()
    {
        var (ctx, tenant, companyId) = CreateTenant();
        await using (ctx)
        {
            ctx.FiscalEvents.Add(new FiscalEvent
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                ModelCode = "303",
                ModelName = "IVA",
                Year = 2026,
                Quarter = 1,
                DeadlineDate = DateTime.UtcNow.AddDays(30),
                ReminderDate = DateTime.UtcNow.AddDays(20),
                Status = "Pending",
            });
            await ctx.SaveChangesAsync();

            var calendar = new FakeFiscalCalendarService();
            var handler = new GenerateFiscalCalendarHandler(ctx, calendar, tenant);
            var result = await handler.Handle(new GenerateFiscalCalendarCommand(2026), CancellationToken.None);

            Assert.Equal(2, result.TotalEvents);
            Assert.Equal(1, result.NewEvents);
            Assert.Equal(2, await ctx.FiscalEvents.CountAsync());
        }
    }

    [Fact]
    public async Task MarkFiscalEventSubmitted_UpdatesStatus()
    {
        var (ctx, tenant, companyId) = CreateTenant();
        await using (ctx)
        {
            var eventId = Guid.NewGuid();
            ctx.FiscalEvents.Add(new FiscalEvent
            {
                Id = eventId,
                CompanyId = companyId,
                ModelCode = "390",
                ModelName = "Resumen anual",
                Year = 2026,
                DeadlineDate = DateTime.UtcNow.AddDays(60),
                ReminderDate = DateTime.UtcNow.AddDays(50),
                Status = "Pending",
            });
            await ctx.SaveChangesAsync();

            var handler = new MarkFiscalEventSubmittedHandler(ctx, tenant);
            var dto = await handler.Handle(new MarkFiscalEventSubmittedCommand(eventId, "REF-123"), CancellationToken.None);

            Assert.NotNull(dto);
            Assert.Equal("Submitted", dto!.Status);
            Assert.Equal("REF-123", dto.SubmissionReference);
        }
    }

    [Fact]
    public async Task CreateFiscalEvent_PersistsManualEvent()
    {
        var (ctx, tenant, companyId) = CreateTenant();
        await using (ctx)
        {
            var handler = new CreateFiscalEventHandler(ctx, tenant);
            var result = await handler.Handle(new CreateFiscalEventCommand(
                "347", "Operaciones terceros", 2026, null, null,
                DateTime.UtcNow.AddDays(90), DateTime.UtcNow.AddDays(80), null, "Manual"), CancellationToken.None);

            Assert.NotEqual(Guid.Empty, result.Id);
            Assert.Single(await ctx.FiscalEvents.ToListAsync());
        }
    }

    [Fact]
    public async Task GetOverdueFiscalEvents_DelegatesToService()
    {
        var (_, tenant, companyId) = CreateTenant();
        var overdueId = Guid.NewGuid();
        var calendar = new FakeFiscalCalendarService
        {
            OverdueEvents =
            [
                new FiscalEvent
                {
                    Id = overdueId,
                    CompanyId = companyId,
                    ModelCode = "303",
                    ModelName = "IVA vencido",
                    Year = 2026,
                    Quarter = 1,
                    DeadlineDate = DateTime.UtcNow.AddDays(-5),
                    ReminderDate = DateTime.UtcNow.AddDays(-15),
                    Status = "Pending",
                },
            ],
        };

        var handler = new GetOverdueFiscalEventsHandler(calendar, tenant);
        var events = await handler.Handle(new GetOverdueFiscalEventsQuery(), CancellationToken.None);

        Assert.Single(events);
        Assert.True(events[0].IsOverdue);
    }
}

public class Phase14VerifyApiKeyHandlerTests
{
    [Fact]
    public async Task VerifyApiKey_ReturnsNull_WhenContextMissing()
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var handler = new VerifyApiKeyHandler(accessor);
        var result = await handler.Handle(new VerifyApiKeyQuery(), CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task VerifyApiKey_ReturnsAuthenticated_WhenItemsPresent()
    {
        var companyId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        httpContext.Items[PublicApiContextKeys.ApiKeyId] = Guid.NewGuid();
        httpContext.Items[PublicApiContextKeys.CompanyId] = companyId;

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var handler = new VerifyApiKeyHandler(accessor);
        var result = await handler.Handle(new VerifyApiKeyQuery(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("authenticated", result!.Status);
        Assert.Equal(companyId, result.CompanyId);
    }
}

public class Phase14SendEmailConfirmationTests
{
    [Fact]
    public async Task SendEmailConfirmation_SendsEmail_WhenNotConfirmed()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"phase14-confirm-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.Users.Add(new User
        {
            Id = userId,
            CompanyId = companyId,
            Email = "confirm@test.com",
            FirstName = "Ana",
            PasswordHash = "hash",
            IsActive = true,
            EmailConfirmed = false,
        });
        await ctx.SaveChangesAsync();

        var email = new FakeEmailService();
        var handler = new SendEmailConfirmationHandler(
            ctx, email, new FakeDistributedCache(), Options.Create(new EmailAuthOptions { AppBaseUrl = "https://app.test" }));

        await handler.Handle(new SendEmailConfirmationCommand(userId), CancellationToken.None);

        Assert.Equal("confirm@test.com", email.LastConfirmationEmail);
    }

    [Fact]
    public async Task SendEmailConfirmation_NoOp_WhenAlreadyConfirmed()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"phase14-confirm-done-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.Users.Add(new User
        {
            Id = userId,
            CompanyId = companyId,
            Email = "done@test.com",
            PasswordHash = "hash",
            IsActive = true,
            EmailConfirmed = true,
        });
        await ctx.SaveChangesAsync();

        var email = new FakeEmailService();
        var handler = new SendEmailConfirmationHandler(
            ctx, email, new FakeDistributedCache(), Options.Create(new EmailAuthOptions()));

        await handler.Handle(new SendEmailConfirmationCommand(userId), CancellationToken.None);

        Assert.Null(email.LastConfirmationEmail);
    }
}
