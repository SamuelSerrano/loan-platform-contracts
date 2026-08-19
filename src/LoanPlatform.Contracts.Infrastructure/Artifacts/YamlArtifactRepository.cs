using LoanPlatform.Contracts.Application.Ports;
using LoanPlatform.Contracts.Domain.Governance;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LoanPlatform.Contracts.Infrastructure.Artifacts;

public sealed class YamlArtifactRepository(string repositoryRoot) : IArtifactRepository
{
    public async Task<GovernanceManifest> LoadManifestAsync(CancellationToken cancellationToken)
    {
        string path = Path.Combine(repositoryRoot, "docs", "contract-governance-manifest.yaml");
        string yaml = await File.ReadAllTextAsync(path, cancellationToken);
        ManifestDto dto = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build()
            .Deserialize<ManifestDto>(yaml);

        ContractDescriptor[] contracts = dto.Contracts.Select(item => new ContractDescriptor(
            item.Name,
            Enum.Parse<ContractCategory>(item.Category),
            item.Location)).ToArray();
        ApprovedFieldPath[] fields = dto.FieldPaths.Select(item => new ApprovedFieldPath(
            item.Contract,
            item.Path,
            ParseClassification(item.Classification),
            item.CanonicalSource)).ToArray();
        return new GovernanceManifest(contracts, fields, dto.DerivedFrom.Commit);
    }

    private static DataClassification ParseClassification(string value) => value switch
    {
        "Restricted secret" => DataClassification.RestrictedSecret,
        _ => Enum.Parse<DataClassification>(value)
    };

    private sealed class ManifestDto
    {
        public required DerivedFromDto DerivedFrom { get; init; }
        public required List<ContractDto> Contracts { get; init; }
        public required List<FieldDto> FieldPaths { get; init; }
    }

    private sealed class DerivedFromDto { public required string Commit { get; init; } }
    private sealed class ContractDto
    {
        public required string Name { get; init; }
        public required string Category { get; init; }
        public required string Location { get; init; }
    }
    private sealed class FieldDto
    {
        public required string Contract { get; init; }
        public required string Path { get; init; }
        public required string Classification { get; init; }
        public required string CanonicalSource { get; init; }
    }
}
