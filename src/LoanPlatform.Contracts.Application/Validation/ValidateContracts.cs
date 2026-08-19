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

        foreach (IExternalSpecificationValidator validator in externalValidators.OrderBy(item => item.GateId, StringComparer.Ordinal))
        {
            findings.AddRange(await validator.ValidateAsync(cancellationToken));
        }

        string compatibility = await compatibilityBaseline.GetStatusAsync(cancellationToken);
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
            findings.OrderBy(finding => finding.Code, StringComparer.Ordinal).ThenBy(finding => finding.Message, StringComparer.Ordinal).ToArray());

        await reportWriter.WriteAsync(report, cancellationToken);
        return report;
    }
}
