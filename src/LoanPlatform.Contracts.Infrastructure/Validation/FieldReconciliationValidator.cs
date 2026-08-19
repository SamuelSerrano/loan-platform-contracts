using System.Text.Json;
using LoanPlatform.Contracts.Application.Ports;
using LoanPlatform.Contracts.Domain.Governance;
using YamlDotNet.RepresentationModel;

namespace LoanPlatform.Contracts.Infrastructure.Validation;

public sealed class FieldReconciliationValidator(string repositoryRoot, IArtifactRepository artifactRepository)
    : IExternalSpecificationValidator
{
    private static readonly string[] EventEnvelope =
    [
        "eventId", "eventType", "eventVersion", "occurredAt", "aggregateId", "correlationId", "causationId",
        "producer", "traceId", "payload"
    ];

    public string GateId => "field-reconciliation-16-175";

    public async Task<ValidationGateResult> ValidateAsync(CancellationToken cancellationToken)
    {
        GovernanceManifest manifest = await artifactRepository.LoadManifestAsync(cancellationToken);
        Dictionary<string, HashSet<string>> canonicalPaths = manifest.FieldPaths
            .GroupBy(field => field.Contract, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(field => field.Path).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
        List<(string Contract, string Path)> discovered = [];
        List<ValidationFinding> findings = [];

        foreach (ContractDescriptor contract in manifest.Contracts)
        {
            using JsonDocument schema = JsonDocument.Parse(await File.ReadAllTextAsync(
                Path.Combine(repositoryRoot, contract.SchemaLocation), cancellationToken));
            List<string> paths = [];
            Discover(schema.RootElement, null, paths, findings, contract.Name, canonicalPaths.GetValueOrDefault(contract.Name, []));

            if (contract.Category == ContractCategory.IntegrationEvent)
            {
                foreach (string envelopePath in EventEnvelope)
                    if (!paths.Remove(envelopePath))
                        findings.Add(new("field.wrong-location", ValidationSeverity.Error,
                            $"{contract.Name} is missing common envelope field {envelopePath}."));
                discovered.AddRange(paths.Select(path => (contract.Name, path)));
            }
            else
            {
                paths.Remove("payload");
                discovered.AddRange(paths.Select(path => (contract.Name, path)));
            }
        }

        foreach (string envelopePath in EventEnvelope)
            discovered.Add(("All initial integration events", envelopePath));
        discovered.AddRange(DiscoverOpenApiPathParameters());
        ValidateOpenApiSchemaBindings(manifest, findings);

        AddDuplicates(discovered, findings);
        HashSet<string> expected = manifest.FieldPaths.Select(Key).ToHashSet(StringComparer.Ordinal);
        HashSet<string> actual = discovered.Select(item => $"{item.Contract}:{item.Path}").ToHashSet(StringComparer.Ordinal);
        foreach (string missing in expected.Except(actual, StringComparer.Ordinal).Order(StringComparer.Ordinal))
            findings.Add(new("field.missing", ValidationSeverity.Error, $"Approved field is not executable: {missing}."));
        foreach (string extra in actual.Except(expected, StringComparer.Ordinal).Order(StringComparer.Ordinal))
            findings.Add(new("field.extra", ValidationSeverity.Error, $"Executable field is not approved at this location: {extra}."));
        return new ValidationGateResult(GateId, "built-in/.NET 10", findings);
    }

    private static string Key(ApprovedFieldPath field) => $"{field.Contract}:{field.Path}";

    private static void Discover(JsonElement schema, string? prefix, ICollection<string> paths,
        ICollection<ValidationFinding> findings, string contract, IReadOnlySet<string> canonicalPaths)
    {
        if (!schema.TryGetProperty("properties", out JsonElement properties)) return;
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (JsonProperty property in properties.EnumerateObject())
        {
            if (!names.Add(property.Name))
                findings.Add(new("field.duplicate", ValidationSeverity.Error, $"Duplicate executable property {contract}:{prefix}{property.Name}."));
            JsonElement child = property.Value;
            bool array = child.TryGetProperty("type", out JsonElement type) && type.ValueKind == JsonValueKind.String && type.GetString() == "array";
            string path = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
            string arrayPath = path + "[]";
            string normalized = array && canonicalPaths.Any(candidate => candidate == arrayPath || candidate.StartsWith(arrayPath + ".", StringComparison.Ordinal))
                ? arrayPath
                : path;
            JsonElement nested = array && child.TryGetProperty("items", out JsonElement items) ? items : child;
            bool hasChildren = nested.TryGetProperty("properties", out _);
            if (!hasChildren || path == "payload") paths.Add(normalized);
            if (hasChildren) Discover(nested, normalized, paths, findings, contract, canonicalPaths);
        }
    }

    private IEnumerable<(string Contract, string Path)> DiscoverOpenApiPathParameters()
    {
        using StreamReader reader = File.OpenText(Path.Combine(repositoryRoot, "openapi", "loan-application-api.v1.yaml"));
        YamlStream yaml = new();
        yaml.Load(reader);
        YamlMappingNode root = (YamlMappingNode)yaml.Documents[0].RootNode;
        YamlMappingNode paths = Map(root, "paths");
        foreach ((YamlNode _, YamlNode pathValue) in paths.Children)
        {
            YamlMappingNode pathItem = (YamlMappingNode)pathValue;
            List<string> shared = Parameters(pathItem, root);
            foreach (string method in new[] { "get", "post", "put", "patch", "delete" })
            {
                if (!pathItem.Children.TryGetValue(new YamlScalarNode(method), out YamlNode? operationNode)) continue;
                YamlMappingNode operation = (YamlMappingNode)operationNode;
                string operationId = Scalar(operation, "operationId");
                foreach (string parameter in shared.Concat(Parameters(operation, root)).Distinct(StringComparer.Ordinal))
                    yield return (operationId, $"path.{parameter}");
            }
        }
    }

    private void ValidateOpenApiSchemaBindings(GovernanceManifest manifest, ICollection<ValidationFinding> findings)
    {
        using StreamReader reader = File.OpenText(Path.Combine(repositoryRoot, "openapi", "loan-application-api.v1.yaml"));
        YamlStream yaml = new();
        yaml.Load(reader);
        YamlMappingNode root = (YamlMappingNode)yaml.Documents[0].RootNode;
        YamlMappingNode components = Map(root, "components");
        YamlMappingNode componentSchemas = Map(components, "schemas");
        Dictionary<string, HashSet<string>> bindings = new(StringComparer.Ordinal);
        foreach (YamlNode pathValue in Map(root, "paths").Children.Values)
        {
            YamlMappingNode pathItem = (YamlMappingNode)pathValue;
            foreach (string method in new[] { "get", "post", "put", "patch", "delete" })
            {
                if (!pathItem.Children.TryGetValue(new YamlScalarNode(method), out YamlNode? operationNode)) continue;
                YamlMappingNode operation = (YamlMappingNode)operationNode;
                string operationId = Scalar(operation, "operationId");
                bindings[operationId] = FindReferences(operation)
                    .Where(reference => reference.StartsWith("#/components/schemas/", StringComparison.Ordinal))
                    .Select(reference => reference.Split('/').Last())
                    .Select(component => Scalar((YamlMappingNode)componentSchemas.Children[new YamlScalarNode(component)], "$ref"))
                    .ToHashSet(StringComparer.Ordinal);
            }
        }

        foreach (ContractDescriptor contract in manifest.Contracts.Where(item => item.Category == ContractCategory.HttpOperation))
        {
            HashSet<string> expectedSections = manifest.FieldPaths.Where(field => field.Contract == contract.Name)
                .Select(field => field.Path.Split('.')[0]).Where(section => section is "request" or "response").ToHashSet(StringComparer.Ordinal);
            bindings.TryGetValue(contract.Name, out HashSet<string>? actualBindings);
            foreach (string section in expectedSections)
            {
                string expected = $"../{contract.SchemaLocation}#/properties/{section}";
                if (actualBindings is null || !actualBindings.Contains(expected))
                    findings.Add(new("field.wrong-location", ValidationSeverity.Error,
                        $"{contract.Name} OpenAPI {section} is not bound to {expected}."));
            }
        }
    }

    private static IEnumerable<string> FindReferences(YamlNode node)
    {
        if (node is YamlMappingNode mapping)
        {
            foreach ((YamlNode key, YamlNode value) in mapping.Children)
            {
                if (((YamlScalarNode)key).Value == "$ref" && value is YamlScalarNode scalar) yield return scalar.Value!;
                foreach (string nested in FindReferences(value)) yield return nested;
            }
        }
        else if (node is YamlSequenceNode sequence)
            foreach (YamlNode value in sequence.Children)
                foreach (string nested in FindReferences(value)) yield return nested;
    }

    private static List<string> Parameters(YamlMappingNode node, YamlMappingNode root)
    {
        if (!node.Children.TryGetValue(new YamlScalarNode("parameters"), out YamlNode? value)) return [];
        List<string> result = [];
        foreach (YamlNode item in (YamlSequenceNode)value)
        {
            if (item is YamlMappingNode mapping && mapping.Children.TryGetValue(new YamlScalarNode("$ref"), out YamlNode? reference))
            {
                string component = ((YamlScalarNode)reference).Value!.Split('/').Last();
                YamlMappingNode parameter = Map(Map(Map(root, "components"), "parameters"), component);
                result.Add(Scalar(parameter, "name"));
            }
            else if (item is YamlMappingNode inline) result.Add(Scalar(inline, "name"));
        }
        return result;
    }

    private static YamlMappingNode Map(YamlMappingNode node, string key) => (YamlMappingNode)node.Children[new YamlScalarNode(key)];
    private static string Scalar(YamlMappingNode node, string key) => ((YamlScalarNode)node.Children[new YamlScalarNode(key)]).Value!;

    private static void AddDuplicates(IEnumerable<(string Contract, string Path)> fields, ICollection<ValidationFinding> findings)
    {
        foreach (string duplicate in fields.Select(item => $"{item.Contract}:{item.Path}").GroupBy(value => value, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1).Select(group => group.Key).Order(StringComparer.Ordinal))
            findings.Add(new("field.duplicate", ValidationSeverity.Error, $"Executable field occurs more than once: {duplicate}."));
    }
}
