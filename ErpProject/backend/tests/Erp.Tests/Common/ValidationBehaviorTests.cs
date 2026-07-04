using Erp.Application.Common.Behaviors;
using FluentValidation;
using MediatR;
using Xunit;

namespace Erp.Tests.Common;

public class ValidationBehaviorTests
{
    private record DummyCommand(string Value) : IRequest<string>;

    private sealed class DummyValidator : AbstractValidator<DummyCommand>
    {
        public DummyValidator() => RuleFor(x => x.Value).NotEmpty().WithMessage("Valor requerido");
    }

    [Fact]
    public async Task Handle_WhenValidationFails_ThrowsValidationException()
    {
        var behavior = new ValidationBehavior<DummyCommand, string>([new DummyValidator()]);
        var called = false;

        await Assert.ThrowsAsync<ValidationException>(() =>
            behavior.Handle(
                new DummyCommand(""),
                _ =>
                {
                    called = true;
                    return Task.FromResult("ok");
                },
                CancellationToken.None));

        Assert.False(called);
    }

    [Fact]
    public async Task Handle_WhenValidationPasses_CallsNext()
    {
        var behavior = new ValidationBehavior<DummyCommand, string>([new DummyValidator()]);
        var result = await behavior.Handle(
            new DummyCommand("valid"),
            _ => Task.FromResult("ok"),
            CancellationToken.None);

        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task Handle_WhenNoValidators_CallsNext()
    {
        var behavior = new ValidationBehavior<DummyCommand, string>([]);
        var result = await behavior.Handle(
            new DummyCommand(""),
            _ => Task.FromResult("ok"),
            CancellationToken.None);

        Assert.Equal("ok", result);
    }
}
