using System.Text.Json;
using Json.Schema;
using LoanPlatform.Contracts.Application.Ports;
using LoanPlatform.Contracts.Domain.Governance;

namespace LoanPlatform.Contracts.Infrastructure.Validation;

public sealed class JsonSchemaExamplesValidator(string repositoryRoot, IArtifactRepository artifactRepository)
    : IExternalSpecificationValidator
{
    public string GateId => "json-schema-examples";

    public async Task<ValidationGateResult> ValidateAsync(CancellationToken cancellationToken)
    {
        List<ValidationFinding> findings = [];
        Dictionary<string, JsonSchema> schemas = new(StringComparer.Ordinal);
        GovernanceManifest manifest = await artifactRepository.LoadManifestAsync(cancellationToken);
        foreach (ContractDescriptor contract in manifest.Contracts)
        {
            JsonSchema schema = LoadSchema(Path.Combine(repositoryRoot, contract.SchemaLocation), schemas);
            string example = Path.Combine(repositoryRoot, "examples", "positive", Slug(contract.Name) + ".json");
            using JsonDocument instance = JsonDocument.Parse(await File.ReadAllTextAsync(example, cancellationToken));
            if (!schema.Evaluate(instance.RootElement, Options()).IsValid)
                findings.Add(new("example.positive-invalid", ValidationSeverity.Error, $"Positive example failed: {Path.GetRelativePath(repositoryRoot, example)}."));
        }

        string expectationsPath = Path.Combine(repositoryRoot, "examples", "negative", "expectations.json");
        NegativeExpectation[] expectations = JsonSerializer.Deserialize<NegativeExpectation[]>(
            await File.ReadAllTextAsync(expectationsPath, cancellationToken), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        foreach (NegativeExpectation expectation in expectations)
        {
            JsonSchema schema = LoadSchema(Path.Combine(repositoryRoot, expectation.Schema), schemas);
            using JsonDocument instance = JsonDocument.Parse(await File.ReadAllTextAsync(
                Path.Combine(repositoryRoot, "examples", "negative", expectation.Example), cancellationToken));
            EvaluationResults result = schema.Evaluate(instance.RootElement, Options());
            bool expected = Flatten(result).Any(item => item.Keyword == expectation.Keyword && item.Location == expectation.Location);
            if (result.IsValid || !expected)
                findings.Add(new("example.negative-unexpected", ValidationSeverity.Error,
                    $"Negative example {expectation.Example} did not fail at {expectation.Location} for {expectation.Keyword}."));
        }
        return new ValidationGateResult(GateId, "JsonSchema.Net 9.4.0 / Draft 2020-12", findings);
    }

    private static EvaluationOptions Options() => new() { OutputFormat = OutputFormat.Hierarchical, RequireFormatValidation = true };
    private static JsonSchema LoadSchema(string path, IDictionary<string, JsonSchema> schemas)
    {
        if (!schemas.TryGetValue(path, out JsonSchema? schema))
        {
            schema = JsonSchema.FromText(File.ReadAllText(path));
            schemas.Add(path, schema);
        }
        return schema;
    }
    private static IEnumerable<(string Keyword, string Location)> Flatten(EvaluationResults result)
    {
        if (result.Errors is not null)
            foreach (string keyword in result.Errors.Keys) yield return (keyword, result.InstanceLocation.ToString());
        foreach (EvaluationResults detail in result.Details ?? [])
            foreach (var failure in Flatten(detail)) yield return failure;
    }
    private static string Slug(string name) => string.Concat(name.Replace(".v1", "").Select((character, index) =>
        char.IsUpper(character) && index > 0 ? $"-{char.ToLowerInvariant(character)}" : char.ToLowerInvariant(character).ToString()));
    private sealed record NegativeExpectation(string Schema, string Example, string Keyword, string Location);
}
