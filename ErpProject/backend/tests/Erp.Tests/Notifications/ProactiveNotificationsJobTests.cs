using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Data;
using Erp.Infrastructure.Jobs;
using Erp.Infrastructure.Services;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Erp.Tests.Notifications;

public class ProactiveNotificationsJobTests
{
    [Theory]
    [InlineData("daily", true)]
    [InlineData("weekly", true)] // depends on day — test Monday explicitly below
    [InlineData("disabled", false)]
    public void ShouldRun_RespectsFrequency(string frequency, bool dailyAlways)
    {
        if (frequency == "daily")
            Assert.True(ProactiveNotificationsJob.ShouldRun("daily", DateTime.UtcNow));
        else if (frequency == "disabled")
            Assert.False(ProactiveNotificationsJob.ShouldRun("disabled", DateTime.UtcNow));
    }

    [Fact]
    public void ShouldRun_WeeklyOnlyOnMonday()
    {
        var monday = new DateTime(2026, 7, 6); // Monday
        var tuesday = new DateTime(2026, 7, 7);
        Assert.True(ProactiveNotificationsJob.ShouldRun("weekly", monday));
        Assert.False(ProactiveNotificationsJob.ShouldRun("weekly", tuesday));
    }

    [Fact]
    public async Task ExecuteAsync_SendsPendingApprovalEmail()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"proactive-{Guid.NewGuid()}")
            .Options;
        await using var ctx = new ErpDbContext(options, tenant);
        ctx.Companies.Add(new Company
        {
            Id = companyId, Name = "Co", TaxId = "B12345678", ProactiveNotificationsFrequency = "daily", Country = "ES"
        });
        ctx.Users.Add(new User
        {
            Id = Guid.NewGuid(), CompanyId = companyId, Email = "admin@test.com",
            PasswordHash = "x", IsActive = true, CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var email = new FakeEmailService();
        var expenses = new FakeAutomationExpensesQuery();
        expenses.PendingApprovals.Add(new AutomationPendingExpenseApproval(Guid.NewGuid(), companyId, "Prov", 200, DateTime.UtcNow));
        var purchasing = new FakeAutomationPurchasingQuery();
        var ruleEvaluator = new Erp.Infrastructure.Automation.RuleEvaluatorJob(
            new FakeAutomationBillingQuery(),
            new FakeAutomationInventoryQuery(),
            email, new DisabledWebPushService(), ctx, NullLogger<Erp.Infrastructure.Automation.RuleEvaluatorJob>.Instance);

        var job = new ProactiveNotificationsJob(
            ctx, expenses, purchasing, email, new DisabledWebPushService(), ruleEvaluator,
            NullLogger<ProactiveNotificationsJob>.Instance);

        await job.ExecuteAsync();

        Assert.Contains(email.Sent, s => s.Subject.Contains("aprobación"));
    }
}

internal sealed class FakeEmailService : IEmailService
{
    public List<(string To, string Subject, string Body)> Sent { get; } = new();

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        Sent.Add((to, subject, htmlBody));
        return Task.CompletedTask;
    }

    public Task SendInvoiceAsync(string toEmail, string toName, string invoiceNumber, DateTime issueDate, DateTime dueDate, decimal total, string companyName, byte[]? pdfAttachment, CancellationToken ct = default) => Task.CompletedTask;
    public Task SendPasswordResetAsync(string toEmail, string toName, string resetUrl, CancellationToken ct = default) => Task.CompletedTask;
    public Task SendEmailConfirmationAsync(string toEmail, string toName, string confirmUrl, CancellationToken ct = default) => Task.CompletedTask;
    public Task SendQuoteAsync(string toEmail, string toName, string quoteNumber, DateTime issueDate, DateTime validUntil, decimal totalAmount, string companyName, string portalUrl, byte[]? pdfAttachment = null, CancellationToken ct = default) => Task.CompletedTask;
    public Task SendWithAttachmentsAsync(string to, string subject, string htmlBody, IReadOnlyList<EmailAttachment> attachments, CancellationToken ct = default) => Task.CompletedTask;
}
