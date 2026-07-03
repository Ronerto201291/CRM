using Erp.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace Erp.Infrastructure.Services;

public sealed class VerifactuModeSettings : IVerifactuModeSettings
{
    private readonly VerifactuOptions _options;

    public VerifactuModeSettings(IOptions<VerifactuOptions> options) =>
        _options = options.Value;

    public bool RealtimeSubmissionEnabled =>
        _options.SubmissionMode != VerifactuSubmissionMode.LocalOnly;
}
