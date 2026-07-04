using Erp.Application.Common.Interfaces;

namespace Erp.Tests.TestSupport;

public sealed class FakeViesService : IViesService
{
    private readonly bool _valid;

    public FakeViesService(bool valid = true) => _valid = valid;

    public Task<ViesValidationResult> ValidateAsync(string countryCode, string vatNumber, CancellationToken ct = default)
        => Task.FromResult(new ViesValidationResult(
            _valid, countryCode, vatNumber, _valid ? "Test Company" : null, null, null, null));
}
