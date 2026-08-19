using System.Text.Json.Nodes;
using LoanPlatform.Contracts.Infrastructure.Validation;

namespace LoanPlatform.Contracts.Infrastructure.Tests;

public sealed class PolicyIntegrityValidatorTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Fact]
    public async Task CanonicalFictitiousPolicySatisfiesEveryCrossFieldInvariant()
    {
        var result = await new PolicyIntegrityValidator(Root).ValidateAsync(CancellationToken.None);
        Assert.True(result.Succeeded);
        Assert.Empty(result.Findings);
    }

    [Theory]
    [InlineData("amount-order", "policy.amount-order")]
    [InlineData("score-order", "policy.score-order")]
    [InlineData("band-overlap", "policy.risk-band-order")]
    [InlineData("band-outside", "policy.risk-band-bounds")]
    [InlineData("term-outside", "policy.term-subset")]
    [InlineData("duplicate-band", "policy.risk-band-duplicate")]
    [InlineData("duplicate-segment", "policy.segment-duplicate")]
    [InlineData("nonpositive-cap", "policy.exposure-cap")]
    [InlineData("invalid-pti", "policy.pti-interval")]
    [InlineData("rate-below-floor", "policy.minimum-rate")]
    [InlineData("alternative-count", "policy.alternative-count")]
    [InlineData("major-version", "policy.major-version")]
    [InlineData("checksum", "policy.checksum")]
    [InlineData("duration", "policy.offer-duration")]
    public async Task TargetedMutationProducesExpectedPolicyFinding(string mutation, string expectedCode)
    {
        JsonNode policy = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(Root, "examples/positive/quick-personal-loan-policy.json")))!;
        ApplyMutation(policy, mutation);
        string copy = Path.Combine(Path.GetTempPath(), $"policy-integrity-{Guid.NewGuid():N}");
        string directory = Path.Combine(copy, "examples", "positive");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "quick-personal-loan-policy.json"), policy.ToJsonString());

        var result = await new PolicyIntegrityValidator(copy).ValidateAsync(CancellationToken.None);

        Assert.Contains(result.Findings, finding => finding.Code == expectedCode);
    }

    private static void ApplyMutation(JsonNode policy, string mutation)
    {
        JsonNode product = policy["product"]!;
        JsonNode risk = policy["risk"]!;
        JsonArray bands = risk["riskBands"]!.AsArray();
        JsonArray segments = policy["alternatives"]!["segmentRules"]!.AsArray();
        switch (mutation)
        {
            case "amount-order": product["minimumAmount"] = 2000; break;
            case "score-order": risk["minimumEligibleScore"] = 0; break;
            case "band-overlap": bands[1]!["maximumScore"] = 750; break;
            case "band-outside": bands[0]!["maximumScore"] = 1001; break;
            case "term-outside": bands[0]!["permittedTermMonths"]!.AsArray().Add(18); break;
            case "duplicate-band": bands[1]!["band"] = "High"; break;
            case "duplicate-segment": segments[1]!["segment"] = "New"; break;
            case "nonpositive-cap": segments[0]!["exposureCap"] = 0; break;
            case "invalid-pti": bands[0]!["paymentToIncomeLimit"] = 1.1; break;
            case "rate-below-floor": bands[2]!["baseMonthlyEffectiveRate"] = 0.015; break;
            case "alternative-count": policy["alternatives"]!["maximumCount"] = 2; break;
            case "major-version": policy["policyVersion"] = "2.0.0"; break;
            case "checksum": policy["checksum"] = "not-a-checksum"; break;
            case "duration": policy["offer"]!["validityDuration"] = "24 hours"; break;
            default: throw new ArgumentOutOfRangeException(nameof(mutation));
        }
    }
}
