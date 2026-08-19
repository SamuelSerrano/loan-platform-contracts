using System.Diagnostics;
using LoanPlatform.Contracts.Application.Ports;

namespace LoanPlatform.Contracts.Infrastructure.Repository;

public sealed class LocalRepositoryMetadata(string repositoryRoot) : IRepositoryMetadata
{
    public string CommitSha => Run("git", "rev-parse HEAD");
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    public string DotNetVersion => Run("dotnet", "--version");

    private string Run(string fileName, string arguments)
    {
        using Process process = Process.Start(new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            UseShellExecute = false
        }) ?? throw new InvalidOperationException($"Could not start {fileName}.");
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode == 0 ? output.Trim() : "unavailable";
    }
}
