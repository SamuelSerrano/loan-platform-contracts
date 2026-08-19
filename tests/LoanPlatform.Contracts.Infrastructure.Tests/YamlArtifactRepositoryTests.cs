using LoanPlatform.Contracts.Application.Ports;
using LoanPlatform.Contracts.Domain.Governance;
using LoanPlatform.Contracts.Infrastructure.Artifacts;
using LoanPlatform.Contracts.Infrastructure.Validation;

namespace LoanPlatform.Contracts.Infrastructure.Tests;

public sealed class YamlArtifactRepositoryTests
{
    [Fact]
    public async Task LoadsExactApprovedBoundary()
    {
        var manifest = await new YamlArtifactRepository(RepositoryRoot()).LoadManifestAsync(CancellationToken.None);

        Assert.Equal(16, manifest.Contracts.Count);
        Assert.Equal(175, manifest.FieldPaths.Count);
        Assert.Equal("b545d085441ce02a61a400f4eb778673410a366d", manifest.ArchitectureSourceCommit);
    }

    [Fact]
    public async Task RemovingOneSchemaFieldReportsTheExactMissingField()
    {
        string copy = CopyArtifacts();
        string schema = Path.Combine(copy, "schemas/common/start-credit-application.schema.json");
        string text = await File.ReadAllTextAsync(schema);
        await File.WriteAllTextAsync(schema, text.Replace("\"productId\": {", "\"removedProductId\": {", StringComparison.Ordinal));

        var findings = (await new FieldReconciliationValidator(copy, new YamlArtifactRepository(copy)).ValidateAsync(CancellationToken.None)).Findings;

        Assert.Contains(findings, item => item.Code == "field.missing" && item.Message.Contains("StartCreditApplication:request.productId", StringComparison.Ordinal));
        Assert.Contains(findings, item => item.Code == "field.extra" && item.Message.Contains("StartCreditApplication:request.removedProductId", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AddingOneSchemaFieldReportsTheExactExtraField()
    {
        string copy = CopyArtifacts();
        string schema = Path.Combine(copy, "schemas/common/standard-problem.schema.json");
        string text = await File.ReadAllTextAsync(schema);
        await File.WriteAllTextAsync(schema, text.Replace("\"traceId\": {", "\"unapproved\": { \"type\": \"string\" },\n    \"traceId\": {", StringComparison.Ordinal));

        var findings = (await new FieldReconciliationValidator(copy, new YamlArtifactRepository(copy)).ValidateAsync(CancellationToken.None)).Findings;

        ValidationFinding finding = Assert.Single(findings, item => item.Code == "field.extra");
        Assert.Contains("StandardProblem:unapproved", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NonzeroOfficialValidatorBecomesFailedGate()
    {
        BoundedProcessValidator validator = new("official-test", "test", RepositoryRoot(), "git",
            ["definitely-not-a-command"], TimeSpan.FromSeconds(10));

        ValidationGateResult result = await validator.ValidateAsync(CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Findings, finding => finding.Code == "validator.failed" && finding.Message.Contains("exited", StringComparison.Ordinal));
    }

    private static string CopyArtifacts()
    {
        string destination = Path.Combine(Path.GetTempPath(), $"contracts-reconciliation-{Guid.NewGuid():N}");
        foreach (string directory in new[] { "docs", "schemas", "openapi" })
            CopyDirectory(Path.Combine(RepositoryRoot(), directory), Path.Combine(destination, directory));
        return destination;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.EnumerateFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        foreach (string directory in Directory.EnumerateDirectories(source)) CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }

    private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
}
