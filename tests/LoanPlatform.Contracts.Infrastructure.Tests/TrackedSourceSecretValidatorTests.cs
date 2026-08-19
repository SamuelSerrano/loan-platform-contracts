using System.Diagnostics;
using LoanPlatform.Contracts.Infrastructure.Validation;

namespace LoanPlatform.Contracts.Infrastructure.Tests;

public sealed class TrackedSourceSecretValidatorTests
{
    [Fact]
    public async Task TrackedSecretFailsWithoutEchoingItsValue()
    {
        string root = Path.Combine(Path.GetTempPath(), $"secret-scan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        RunGit(root, "init");
        string secret = "gh" + "p_" + new string('A', 24);
        await File.WriteAllTextAsync(Path.Combine(root, "credential.txt"), secret);
        RunGit(root, "add", "credential.txt");

        var result = await new TrackedSourceSecretValidator(root).ValidateAsync(CancellationToken.None);

        Assert.False(result.Succeeded);
        var finding = Assert.Single(result.Findings, item => item.Code == "secret-scan.detected");
        Assert.Contains("credential.txt", finding.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, finding.Message, StringComparison.Ordinal);
    }

    private static void RunGit(string root, params string[] arguments)
    {
        ProcessStartInfo start = new("git") { WorkingDirectory = root, UseShellExecute = false };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start)!;
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
    }
}
