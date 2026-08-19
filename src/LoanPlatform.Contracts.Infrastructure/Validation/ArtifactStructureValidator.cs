using System.Text.Json;
using LoanPlatform.Contracts.Application.Ports;
using LoanPlatform.Contracts.Domain.Governance;

namespace LoanPlatform.Contracts.Infrastructure.Validation;

public sealed class ArtifactStructureValidator(string repositoryRoot) : IExternalSpecificationValidator
{
    public string GateId => "closed-json-schemas";

    public Task<ValidationGateResult> ValidateAsync(CancellationToken cancellationToken)
    {
        List<ValidationFinding> findings = [];
        foreach (string path in Directory.EnumerateFiles(Path.Combine(repositoryRoot, "schemas"), "*.json", SearchOption.AllDirectories))
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            ValidateNode(document.RootElement, Path.GetRelativePath(repositoryRoot, path), "#", findings);
        }
        return Task.FromResult(new ValidationGateResult(GateId, "built-in/.NET 10", findings));
    }

    private static void ValidateNode(JsonElement node, string file, string pointer, ICollection<ValidationFinding> findings)
    {
        if (node.ValueKind != JsonValueKind.Object) return;
        bool hasStringType = node.TryGetProperty("type", out JsonElement type) && type.ValueKind == JsonValueKind.String;
        if (hasStringType && type.GetString() == "object")
        {
            if (!node.TryGetProperty("additionalProperties", out JsonElement closed) || closed.ValueKind != JsonValueKind.False)
                findings.Add(new("schema.open-object", ValidationSeverity.Error, $"{file}{pointer} must set additionalProperties to false."));
        }
        if (hasStringType && type.GetString() == "array" && !node.TryGetProperty("items", out _))
            findings.Add(new("schema.array-items", ValidationSeverity.Error, $"{file}{pointer} must define items."));
        foreach (JsonProperty property in node.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Object)
                ValidateNode(property.Value, file, $"{pointer}/{property.Name}", findings);
            else if (property.Value.ValueKind == JsonValueKind.Array)
                foreach (JsonElement child in property.Value.EnumerateArray())
                    ValidateNode(child, file, $"{pointer}/{property.Name}", findings);
        }
    }
}
