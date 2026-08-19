using System.Text.Json;
using Json.Schema;
using LoanPlatform.Contracts.Infrastructure.Artifacts;

namespace LoanPlatform.Contracts.ContractTests;

public sealed class ContractArtifactTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    private static readonly Dictionary<string, JsonSchema> Schemas = new(StringComparer.Ordinal);

    [Fact]
    public async Task EveryApprovedContractHasSchemaAndValidPositiveExample()
    {
        var manifest = await new YamlArtifactRepository(Root).LoadManifestAsync(CancellationToken.None);
        Assert.Equal(16, manifest.Contracts.Count);

        foreach (var contract in manifest.Contracts)
        {
            string schemaPath = Path.Combine(Root, contract.SchemaLocation);
            string examplePath = Path.Combine(Root, "examples", "positive", Slug(contract.Name) + ".json");
            JsonSchema schema = LoadSchema(schemaPath);
            using JsonDocument instance = JsonDocument.Parse(await File.ReadAllTextAsync(examplePath));
            Assert.True(schema.Evaluate(instance.RootElement).IsValid, contract.Name);
        }
    }

    [Theory]
    [InlineData("schemas/common/start-credit-application.schema.json", "examples/negative/start-credit-application-unknown-field.json")]
    [InlineData("schemas/events/credit-application-submitted.v1.schema.json", "examples/negative/credit-application-submitted-missing-payload.json")]
    public async Task NegativeExamplesAreRejected(string schemaRelativePath, string exampleRelativePath)
    {
        JsonSchema schema = LoadSchema(Path.Combine(Root, schemaRelativePath));
        using JsonDocument instance = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Root, exampleRelativePath)));
        Assert.False(schema.Evaluate(instance.RootElement).IsValid);
    }

    [Fact]
    public void PublicContractsDoNotExposeJwtOrForbiddenDecisionInternals()
    {
        string publicText = string.Join('\n', Directory.EnumerateFiles(Path.Combine(Root, "openapi")).Concat(
            Directory.EnumerateFiles(Path.Combine(Root, "schemas", "common"), "*.json")).Select(File.ReadAllText));
        Assert.DoesNotContain("rawToken", publicText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("jwtPayload", publicText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rulesEvaluated", publicText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internalScore", publicText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProducerAndConsumerUseDistinctPrivateTypesAcrossJsonBoundary()
    {
        ProducerFixture produced = new("application-123", "customer-456");
        string json = JsonSerializer.Serialize(produced, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        ConsumerFixture consumed = JsonSerializer.Deserialize<ConsumerFixture>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.NotEqual(typeof(ProducerFixture), typeof(ConsumerFixture));
        Assert.Equal(produced.ApplicationId, consumed.ApplicationId);
        Assert.Equal(produced.CustomerId, consumed.CustomerId);
    }

    private static string Slug(string name) => string.Concat(name.Replace(".v1", "").Select((character, index) =>
        char.IsUpper(character) && index > 0 ? $"-{char.ToLowerInvariant(character)}" : char.ToLowerInvariant(character).ToString()));

    private static JsonSchema LoadSchema(string path)
    {
        if (!Schemas.TryGetValue(path, out JsonSchema? schema))
        {
            schema = JsonSchema.FromText(File.ReadAllText(path));
            Schemas.Add(path, schema);
        }
        return schema;
    }

    private sealed record ProducerFixture(string ApplicationId, string CustomerId);
    private sealed record ConsumerFixture(string ApplicationId, string CustomerId);
}
