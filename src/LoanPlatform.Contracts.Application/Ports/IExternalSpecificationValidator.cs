using LoanPlatform.Contracts.Domain.Governance;

namespace LoanPlatform.Contracts.Application.Ports;

public interface IExternalSpecificationValidator
{
    string GateId { get; }

    Task<ValidationGateResult> ValidateAsync(CancellationToken cancellationToken);
}

public sealed record ValidationGateResult(
    string GateId,
    string ValidatorVersion,
    IReadOnlyList<ValidationFinding> Findings)
{
    public bool Succeeded => Findings.All(finding => finding.Severity != ValidationSeverity.Error);
}
