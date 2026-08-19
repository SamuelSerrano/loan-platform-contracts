using LoanPlatform.Contracts.Application.Ports;
using LoanPlatform.Contracts.Domain.Governance;

namespace LoanPlatform.Contracts.Application.Validation;

public sealed class ValidateContracts(
    IArtifactRepository artifactRepository,
    IReadOnlyList<IExternalSpecificationValidator> externalValidators,
    ICompatibilityBaseline compatibilityBaseline,
    IValidationReportWriter reportWriter,
    IRepositoryMetadata repositoryMetadata)
{
    public async Task<ValidationReport> ExecuteAsync(CancellationToken cancellationToken)
    {
        GovernanceManifest manifest = await artifactRepository.LoadManifestAsync(cancellationToken);
        List<ValidationFinding> findings = [.. ContractInventory.Validate(manifest.Contracts, manifest.FieldPaths)];
        List<ValidationGateResult> gates = [];

        foreach (IExternalSpecificationValidator validator in externalValidators.OrderBy(item => item.GateId, StringComparer.Ordinal))
        {
            ValidationGateResult gate;
            try
            {
                gate = await validator.ValidateAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                gate = new ValidationGateResult(validator.GateId, "unavailable",
                    [new ValidationFinding("validator.exception", ValidationSeverity.Error, $"{validator.GateId} failed technically: {exception.GetType().Name}.")]);
            }
            gates.Add(gate);
            findings.AddRange(gate.Findings);
        }

        string compatibility = await compatibilityBaseline.GetStatusAsync(cancellationToken);
        gates.Add(new ValidationGateResult("compatibility", compatibility, []));
        ValidationReport report = new(
            repositoryMetadata.CommitSha,
            repositoryMetadata.UtcNow.ToUniversalTime(),
            repositoryMetadata.DotNetVersion,
            manifest.ArchitectureSourceCommit,
            "3.1.2",
            "3.1.0",
            "https://json-schema.org/draft/2020-12/schema",
            manifest.Contracts.Count,
            manifest.FieldPaths.Count,
            compatibility,
            gates.OrderBy(gate => gate.GateId, StringComparer.Ordinal).ToArray(),
            findings.OrderBy(finding => finding.Code, StringComparer.Ordinal).ThenBy(finding => finding.Message, StringComparer.Ordinal).ToArray());

        await reportWriter.WriteAsync(report, cancellationToken);
        return report;
    }
}
