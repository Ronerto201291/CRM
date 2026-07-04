using Erp.Application.Common.Events;
using Erp.Application.Common.Interfaces;
using Erp.Application.Features.Auth.Commands;
using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Data;
using Erp.Tests.TestSupport;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Auth;

public class RegisterCompanyHandlerTests
{
    [Fact]
    public async Task Handle_CreatesCompanyUserAndMembership()
    {
        var tenant = new FakeTenantContext();
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"register-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        var mediator = new NoOpMediator();
        var handler = new RegisterCompanyHandler(ctx, new FakeJwtProvider(), mediator);

        var result = await handler.Handle(new RegisterCompanyCommand
        {
            CompanyName = "Startup SL",
            CompanyTaxId = "B33333333",
            CompanyAddress = "Calle Nueva 1",
            AdminEmail = "admin@startup.test",
            AdminPassword = "SecurePass123!",
            AdminFirstName = "Ana",
            AdminLastName = "García",
        }, CancellationToken.None);

        Assert.Equal("Startup SL", result.CompanyName);
        Assert.Equal("admin@startup.test", result.Email);
        Assert.False(string.IsNullOrEmpty(result.Token));

        var company = await ctx.Companies.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TaxId == "B33333333");
        Assert.NotNull(company);

        var user = await ctx.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == "admin@startup.test");
        Assert.NotNull(user);

        var membership = await ctx.UserCompanies.IgnoreQueryFilters()
            .FirstOrDefaultAsync(uc => uc.UserId == user!.Id && uc.CompanyId == company!.Id);
        Assert.NotNull(membership);
        Assert.True(membership!.IsDefault);

        var published = Assert.IsType<CompanyCreatedEvent>(Assert.Single(mediator.Published));
        Assert.Equal(company!.Id, published.CompanyId);
    }

    [Fact]
    public async Task Handle_DuplicateTaxId_Throws()
    {
        var tenant = new FakeTenantContext();
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"register-dup-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.Companies.Add(new Company
        {
            Id = Guid.NewGuid(),
            Name = "Existente",
            TaxId = "B44444444",
            IsActive = true,
            Country = "ES",
        });
        await ctx.SaveChangesAsync();

        var handler = new RegisterCompanyHandler(ctx, new FakeJwtProvider(), new NoOpMediator());

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new RegisterCompanyCommand
            {
                CompanyName = "Otra SL",
                CompanyTaxId = "B44444444",
                AdminEmail = "otro@test.com",
                AdminPassword = "pass",
                AdminFirstName = "X",
            },
            CancellationToken.None));
    }

    private sealed class FakeJwtProvider : IJwtProvider
    {
        public string Generate(User user, Guid? activeCompanyId = null) => "test-jwt-token";
    }

    private sealed class NoOpMediator : IMediator
    {
        public List<object> Published { get; } = [];

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            => Task.FromResult<TResponse>(default!);

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
            => Task.CompletedTask;

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            => Task.FromResult<object?>(null);

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => AsyncEnumerable.Empty<TResponse>();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => AsyncEnumerable.Empty<object?>();

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            Published.Add(notification);
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
        {
            Published.Add(notification!);
            return Task.CompletedTask;
        }
    }
}
