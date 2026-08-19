using System.Diagnostics;
using LoanPlatform.Contracts.Application.Ports;
using LoanPlatform.Contracts.Domain.Governance;

namespace LoanPlatform.Contracts.Infrastructure.Validation;

public sealed class BoundedProcessValidator(
    string gateId,
    string validatorVersion,
    string repositoryRoot,
    string executable,
    IReadOnlyList<string> arguments,
    TimeSpan timeout) : IExternalSpecificationValidator
{
    public string GateId => gateId;

    public async Task<ValidationGateResult> ValidateAsync(CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        ProcessStartInfo start = new(executable) { WorkingDirectory = repositoryRoot, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {executable}.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(timeoutSource.Token);
        Task<string> stderr = process.StandardError.ReadToEndAsync(timeoutSource.Token);
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            return Failure("validator.timeout", $"{gateId} exceeded {timeout.TotalSeconds:0} seconds.");
        }
        await stdout;
        await stderr;
        return process.ExitCode == 0
            ? new ValidationGateResult(GateId, validatorVersion, [])
            : Failure("validator.failed", $"{gateId} exited with code {process.ExitCode}; tool output is intentionally omitted from evidence.");
    }

    private ValidationGateResult Failure(string code, string message) =>
        new(GateId, validatorVersion, [new ValidationFinding(code, ValidationSeverity.Error, message)]);
}
