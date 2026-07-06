namespace Erp.IntegrationTests;

/// <summary>Helpers for Testcontainers-based integration tests (ADR-0018 #70).</summary>
internal static class IntegrationTestDocker
{
    /// <summary>True when CI expects Docker (Testcontainers) to be available.</summary>
    internal static bool IsCiEnvironment =>
        string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase);

    internal static bool IsDockerUnavailable(Exception ex) =>
        ex.Message.Contains("Docker", StringComparison.OrdinalIgnoreCase)
        || ex.GetType().FullName?.Contains("Docker", StringComparison.OrdinalIgnoreCase) == true
        || (ex.InnerException is not null && IsDockerUnavailable(ex.InnerException));

    internal static void HandleDockerUnavailable(Exception ex)
    {
        if (!IsDockerUnavailable(ex))
            throw ex;

        if (IsCiEnvironment)
            throw new InvalidOperationException(
                "Docker/Testcontainers is required in CI but unavailable. " + ex.Message, ex);

        // Local dev without Docker: exit without Assert.True(true) fake verification (ADR-0018 #70).
    }
}
