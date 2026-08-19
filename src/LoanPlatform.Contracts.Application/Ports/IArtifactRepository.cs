using LoanPlatform.Contracts.Domain.Governance;

namespace LoanPlatform.Contracts.Application.Ports;

public sealed record GovernanceManifest(
    IReadOnlyList<ContractDescriptor> Contracts,
    IReadOnlyList<ApprovedFieldPath> FieldPaths,
    string ArchitectureSourceCommit);

public interface IArtifactRepository
{
    Task<GovernanceManifest> LoadManifestAsync(CancellationToken cancellationToken);
}
