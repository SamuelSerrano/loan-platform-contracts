using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using LoanPlatform.Contracts.Application.Ports;
using LoanPlatform.Contracts.Domain.Governance;

namespace LoanPlatform.Contracts.Infrastructure.Validation;

public sealed class TrackedSourceSecretValidator(string repositoryRoot) : IExternalSpecificationValidator
{
    private static readonly IReadOnlyList<(string Id, Regex Pattern)> Patterns =
    [
        ("github-token", new Regex("gh" + "[pousr]_" + "[A-Za-z0-9]{20,}", RegexOptions.CultureInvariant)),
        ("aws-access-key", new Regex("AKIA" + "[0-9A-Z]{16}", RegexOptions.CultureInvariant)),
        ("private-key", new Regex("BEGIN " + "(RSA|OPENSSH|EC) PRIVATE KEY", RegexOptions.CultureInvariant))
    ];

    public string GateId => "tracked-source-secret-scan";

    public async Task<ValidationGateResult> ValidateAsync(CancellationToken cancellationToken)
    {
        ProcessStartInfo start = new("git")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add("ls-files");
        start.ArgumentList.Add("-z");
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Could not start git.");
        string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
            return Result([new("secret-scan.git-failed", ValidationSeverity.Error, "Could not enumerate tracked sources; git output was omitted.")]);

        List<ValidationFinding> findings = [];
        foreach (string relativePath in output.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            string path = Path.Combine(repositoryRoot, relativePath);
            string text;
            try { text = await File.ReadAllTextAsync(path, cancellationToken); }
            catch (DecoderFallbackException) { continue; }
            foreach ((string id, Regex pattern) in Patterns)
                if (pattern.IsMatch(text))
                    findings.Add(new("secret-scan.detected", ValidationSeverity.Error,
                        $"Tracked file {relativePath} matched prohibited secret pattern {id}; value omitted."));
        }
        return Result(findings);
    }

    private ValidationGateResult Result(IReadOnlyList<ValidationFinding> findings) =>
        new(GateId, "tracked-source secret patterns v1", findings);
}
