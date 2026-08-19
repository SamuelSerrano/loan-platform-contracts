using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    [InlineData("schemas/common/start-credit-application.schema.json", "examples/negative/start-credit-application-unknown-field.json", "additionalProperties", "/request")]
    [InlineData("schemas/events/credit-application-submitted.v1.schema.json", "examples/negative/credit-application-submitted-missing-payload.json", "required", "")]
    [InlineData("schemas/events/credit-application-submitted.v1.schema.json", "examples/negative/event-version-zero.json", "const", "/eventVersion")]
    [InlineData("schemas/events/credit-application-submitted.v1.schema.json", "examples/negative/event-version-unknown.json", "const", "/eventVersion")]
    [InlineData("schemas/events/credit-application-submitted.v1.schema.json", "examples/negative/event-wrong-type.json", "const", "/eventType")]
    [InlineData("schemas/events/credit-application-submitted.v1.schema.json", "examples/negative/event-root-additional-field.json", "additionalProperties", "")]
    [InlineData("schemas/events/credit-application-submitted.v1.schema.json", "examples/negative/event-nested-additional-field.json", "additionalProperties", "/payload")]
    [InlineData("schemas/events/credit-application-submitted.v1.schema.json", "examples/negative/event-invalid-timestamp.json", "pattern", "/occurredAt")]
    [InlineData("schemas/events/credit-application-submitted.v1.schema.json", "examples/negative/event-non-z-timestamp.json", "pattern", "/occurredAt")]
    [InlineData("schemas/requests/credit-assessment-requested.v1.schema.json", "examples/negative/request-wrong-type.json", "const", "/requestType")]
    [InlineData("schemas/requests/credit-assessment-requested.v1.schema.json", "examples/negative/request-unsupported-version.json", "const", "/requestVersion")]
    [InlineData("schemas/requests/credit-assessment-requested.v1.schema.json", "examples/negative/request-identity-not-verified.json", "const", "/payload/identityVerificationStatus")]
    [InlineData("schemas/common/standard-problem.schema.json", "examples/negative/standard-problem-reason-code-leak.json", "additionalProperties", "")]
    [InlineData("schemas/common/standard-problem.schema.json", "examples/negative/standard-problem-invalid-status.json", "maximum", "/status")]
    [InlineData("schemas/events/credit-offer-created.v1.schema.json", "examples/negative/offer-invalid-terms-hash.json", "pattern", "/payload/termsHash")]
    [InlineData("schemas/policies/quick-personal-loan-policy.v1.schema.json", "examples/negative/policy-unsupported-major-version.json", "pattern", "/policyVersion")]
    public async Task NegativeExamplesFailForExpectedKeywordAndLocation(
        string schemaRelativePath, string exampleRelativePath, string keyword, string location)
    {
        JsonSchema schema = LoadSchema(Path.Combine(Root, schemaRelativePath));
        using JsonDocument instance = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Root, exampleRelativePath)));
        EvaluationResults result = schema.Evaluate(instance.RootElement, new EvaluationOptions
        {
            OutputFormat = OutputFormat.Hierarchical,
            RequireFormatValidation = true
        });
        Assert.False(result.IsValid);
        Assert.Contains(Flatten(result), item => item.Keyword == keyword && item.Location == location);
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
    public void WalkingSkeletonUsesSchemaBetweenIndependentProducerAndConsumerModels()
    {
        ProducerFixture produced = ProducerFixture.Valid();
        string json = JsonSerializer.Serialize(produced, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.True(LoadSchema(Path.Combine(Root, "schemas/events/credit-application-submitted.v1.schema.json"))
            .Evaluate(document.RootElement, new EvaluationOptions { RequireFormatValidation = true }).IsValid);
        ConsumerFixture consumed = JsonSerializer.Deserialize<ConsumerFixture>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.NotEqual(typeof(ProducerFixture), typeof(ConsumerFixture));
        Assert.Equal(produced.Payload.ApplicationId, consumed.Payload.ApplicationId);
        Assert.Equal(produced.Payload.CustomerId, consumed.Payload.CustomerId);
    }

    [Fact]
    public void ConsumerDistinguishesDuplicateChangedReplayUnsupportedVersionAndMalformedMessage()
    {
        WalkingSkeletonReceiver receiver = new(LoadSchema(Path.Combine(Root, "schemas/events/credit-application-submitted.v1.schema.json")));
        string valid = JsonSerializer.Serialize(ProducerFixture.Valid(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        JsonNode changed = JsonNode.Parse(valid)!;
        changed["payload"]!["productId"] = "changed-product";
        JsonNode unsupported = JsonNode.Parse(valid)!;
        unsupported["eventVersion"] = 2;

        Assert.Equal(ReceiveResult.Accepted, receiver.Receive(valid));
        Assert.Equal(ReceiveResult.Duplicate, receiver.Receive(valid));
        Assert.Equal(ReceiveResult.ChangedReplay, receiver.Receive(changed.ToJsonString()));
        Assert.Equal(ReceiveResult.UnsupportedVersion, receiver.Receive(unsupported.ToJsonString()));
        Assert.Equal(ReceiveResult.Malformed, receiver.Receive("{\"eventId\":"));
    }

    [Theory]
    [InlineData("credit-assessment-pending-evidence.json", "CreditAssessmentPendingEvidence.v1")]
    [InlineData("credit-assessment-pending-retry.json", "CreditAssessmentPendingRetry.v1")]
    [InlineData("credit-assessment-operational-exception-recorded.json", "CreditAssessmentOperationalExceptionRecorded.v1")]
    [InlineData("favorable-credit-decision-recorded.json", "FavorableCreditDecisionRecorded.v1")]
    [InlineData("unfavorable-credit-decision-recorded.json", "UnfavorableCreditDecisionRecorded.v1")]
    public void DispositionsAndOutcomesRemainSeparateWireFacts(string example, string expectedType)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "examples", "positive", example)));
        Assert.Equal(expectedType, document.RootElement.GetProperty("eventType").GetString());
    }

    [Fact]
    public void ImmutableOfferRejectsChangedTermsReplayWithSameOfferIdentityAndHash()
    {
        string json = File.ReadAllText(Path.Combine(Root, "examples", "positive", "credit-offer-created.json"));
        JsonNode changed = JsonNode.Parse(json)!;
        changed["payload"]!["terms"]!["amount"] = 9999;
        ImmutableOfferReceiver receiver = new(LoadSchema(Path.Combine(Root, "schemas/events/credit-offer-created.v1.schema.json")));

        Assert.Equal(ReceiveResult.Accepted, receiver.Receive(json));
        Assert.Equal(ReceiveResult.ChangedReplay, receiver.Receive(changed.ToJsonString()));
    }

    [Fact]
    public void AsyncApiDeclaresOnlyApprovedOwnershipAndKeepsRequestOutsideIntegrationEvents()
    {
        string events = File.ReadAllText(Path.Combine(Root, "asyncapi", "integration-events.v1.yaml"));
        string request = File.ReadAllText(Path.Combine(Root, "asyncapi", "credit-assessment-requests.v1.yaml"));
        Assert.Contains("x-producer: Application Process", events, StringComparison.Ordinal);
        Assert.Contains("x-producer: Customer & Identity", events, StringComparison.Ordinal);
        Assert.Contains("x-producer: Credit Decisioning", events, StringComparison.Ordinal);
        Assert.DoesNotContain("CreditAssessmentRequested", events, StringComparison.Ordinal);
        Assert.Contains("not an integration event and no IE identifier", request, StringComparison.Ordinal);
        Assert.Contains("x-known-consumers: [Credit Decisioning]", request, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryOpenApiErrorResponseUsesTheValidatedStandardProblem()
    {
        string openApi = File.ReadAllText(Path.Combine(Root, "openapi", "loan-application-api.v1.yaml"));
        Assert.Equal(12, openApi.Split("$ref: '#/components/responses/", StringSplitOptions.None).Length - 1);
        Assert.Contains("$ref: ../schemas/common/standard-problem.schema.json", openApi, StringComparison.Ordinal);
        using JsonDocument problem = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "examples", "positive", "standard-problem.json")));
        Assert.True(LoadSchema(Path.Combine(Root, "schemas/common/standard-problem.schema.json")).Evaluate(problem.RootElement).IsValid);
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

    private static IEnumerable<(string Keyword, string Location)> Flatten(EvaluationResults result)
    {
        if (result.Errors is not null)
            foreach (string keyword in result.Errors.Keys)
                yield return (keyword, result.InstanceLocation.ToString());
        foreach (EvaluationResults detail in result.Details ?? [])
            foreach (var failure in Flatten(detail)) yield return failure;
    }

    private sealed record ProducerFixture(
        string EventId, string EventType, int EventVersion, string OccurredAt, string AggregateId,
        string CorrelationId, string CausationId, string Producer, string TraceId, ProducerPayload Payload)
    {
        public static ProducerFixture Valid() => new("event-1", "CreditApplicationSubmitted.v1", 1,
            "2026-08-19T12:00:00Z", "application-123", "correlation-1", "command-1",
            "ApplicationProcess", "trace-1", new("application-123", "customer-456", "quick-loan", ["consent-1"], "2026-08-19T12:00:00Z"));
    }
    private sealed record ProducerPayload(string ApplicationId, string CustomerId, string ProductId, string[] ConsentReferenceIds, string SubmittedAt);
    private sealed record ConsumerFixture(
        string EventId, string EventType, int EventVersion, string OccurredAt, string AggregateId,
        string CorrelationId, string CausationId, string Producer, string TraceId, ConsumerPayload Payload);
    private sealed record ConsumerPayload(string ApplicationId, string CustomerId, string ProductId, IReadOnlyList<string> ConsentReferenceIds, string SubmittedAt);

    private sealed class WalkingSkeletonReceiver(JsonSchema schema)
    {
        private readonly Dictionary<string, string> hashes = new(StringComparer.Ordinal);
        public ReceiveResult Receive(string json)
        {
            JsonDocument document;
            try { document = JsonDocument.Parse(json); }
            catch (JsonException) { return ReceiveResult.Malformed; }
            using (document)
            {
                if (!document.RootElement.TryGetProperty("eventVersion", out JsonElement version) || version.GetInt32() != 1)
                    return ReceiveResult.UnsupportedVersion;
                if (!schema.Evaluate(document.RootElement, new EvaluationOptions { RequireFormatValidation = true }).IsValid)
                    return ReceiveResult.Malformed;
                string eventId = document.RootElement.GetProperty("eventId").GetString()!;
                string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
                if (!hashes.TryAdd(eventId, hash)) return hashes[eventId] == hash ? ReceiveResult.Duplicate : ReceiveResult.ChangedReplay;
                return ReceiveResult.Accepted;
            }
        }
    }
    private sealed class ImmutableOfferReceiver(JsonSchema schema)
    {
        private readonly Dictionary<string, string> snapshots = new(StringComparer.Ordinal);
        public ReceiveResult Receive(string json)
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (!schema.Evaluate(document.RootElement, new EvaluationOptions { RequireFormatValidation = true }).IsValid)
                return ReceiveResult.Malformed;
            JsonElement payload = document.RootElement.GetProperty("payload");
            string offerId = payload.GetProperty("offerId").GetString()!;
            string snapshot = payload.GetProperty("terms").GetRawText() + payload.GetProperty("termsHash").GetString();
            if (!snapshots.TryAdd(offerId, snapshot)) return snapshots[offerId] == snapshot ? ReceiveResult.Duplicate : ReceiveResult.ChangedReplay;
            return ReceiveResult.Accepted;
        }
    }
    private enum ReceiveResult { Accepted, Duplicate, ChangedReplay, UnsupportedVersion, Malformed }
}
