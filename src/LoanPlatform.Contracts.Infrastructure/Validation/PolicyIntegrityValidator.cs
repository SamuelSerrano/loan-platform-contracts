using System.Text.Json;
using System.Text.RegularExpressions;
using LoanPlatform.Contracts.Application.Ports;
using LoanPlatform.Contracts.Domain.Governance;

namespace LoanPlatform.Contracts.Infrastructure.Validation;

public sealed class PolicyIntegrityValidator(string repositoryRoot) : IExternalSpecificationValidator
{
    public string GateId => "policy-integrity";

    public async Task<ValidationGateResult> ValidateAsync(CancellationToken cancellationToken)
    {
        string path = Path.Combine(repositoryRoot, "examples", "positive", "quick-personal-loan-policy.json");
        using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(path, cancellationToken));
        JsonElement root = document.RootElement;
        List<ValidationFinding> findings = [];
        JsonElement product = root.GetProperty("product");
        JsonElement risk = root.GetProperty("risk");
        JsonElement alternatives = root.GetProperty("alternatives");

        if (Decimal(product, "minimumAmount") >= Decimal(product, "maximumAmount"))
            Add(findings, "policy.amount-order", "minimumAmount must be less than maximumAmount.");

        int minimumValid = Integer(risk, "minimumValidScore");
        int minimumEligible = Integer(risk, "minimumEligibleScore");
        int maximumValid = Integer(risk, "maximumValidScore");
        if (!(minimumValid < minimumEligible && minimumEligible <= maximumValid))
            Add(findings, "policy.score-order", "Score bounds must satisfy minimumValid < minimumEligible <= maximumValid.");

        HashSet<int> supportedTerms = product.GetProperty("supportedTermMonths").EnumerateArray().Select(item => item.GetInt32()).ToHashSet();
        JsonElement[] bands = risk.GetProperty("riskBands").EnumerateArray().ToArray();
        if (bands.Select(item => item.GetProperty("band").GetString()).Distinct(StringComparer.Ordinal).Count() != bands.Length)
            Add(findings, "policy.risk-band-duplicate", "Risk bands must be unique.");
        int priorMinimum = maximumValid + 1;
        foreach (JsonElement band in bands)
        {
            int minimum = Integer(band, "minimumScore");
            int maximum = Integer(band, "maximumScore");
            if (minimum < minimumValid || maximum > maximumValid || minimum > maximum)
                Add(findings, "policy.risk-band-bounds", "Every risk band must be closed and remain within valid score bounds.");
            if (maximum >= priorMinimum)
                Add(findings, "policy.risk-band-order", "Risk bands must be ordered and non-overlapping.");
            priorMinimum = minimum;
            if (band.GetProperty("permittedTermMonths").EnumerateArray().Any(term => !supportedTerms.Contains(term.GetInt32())))
                Add(findings, "policy.term-subset", "Every band term must belong to product.supportedTermMonths.");
            if (Decimal(band, "exposureCap") <= 0)
                Add(findings, "policy.exposure-cap", "Risk exposure caps must be positive.");
            decimal pti = Decimal(band, "paymentToIncomeLimit");
            if (pti <= 0 || pti > 1)
                Add(findings, "policy.pti-interval", "PTI limits must be greater than zero and at most one.");
        }

        JsonElement[] segments = alternatives.GetProperty("segmentRules").EnumerateArray().ToArray();
        if (segments.Select(item => item.GetProperty("segment").GetString()).Distinct(StringComparer.Ordinal).Count() != segments.Length)
            Add(findings, "policy.segment-duplicate", "Segment rules must be unique.");
        if (segments.Any(segment => Decimal(segment, "exposureCap") <= 0))
            Add(findings, "policy.exposure-cap", "Segment exposure caps must be positive.");

        decimal minimumRate = Decimal(root.GetProperty("pricing"), "minimumMonthlyEffectiveRate");
        if (bands.Any(band => segments.Any(segment =>
                Decimal(band, "baseMonthlyEffectiveRate") + Decimal(segment, "rateAdjustmentPercentagePoints") < minimumRate)))
            Add(findings, "policy.minimum-rate", "Base rate plus every segment adjustment must respect the configured minimum.");

        int maximumCount = Integer(alternatives, "maximumCount");
        int objectiveCount = alternatives.GetProperty("objectives").GetArrayLength();
        if (maximumCount != objectiveCount || maximumCount > 3)
            Add(findings, "policy.alternative-count", "maximumCount must equal the approved objective count and be at most three.");

        string version = root.GetProperty("policyVersion").GetString()!;
        if (!Regex.IsMatch(version, "^1\\.\\d+\\.\\d+$", RegexOptions.CultureInvariant))
            Add(findings, "policy.major-version", "Policy version must remain immutable major version 1.");
        string checksum = root.GetProperty("checksum").GetString()!;
        if (!Regex.IsMatch(checksum, "^sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant))
            Add(findings, "policy.checksum", "Checksum must be a lowercase sha256 digest.");
        string duration = root.GetProperty("offer").GetProperty("validityDuration").GetString()!;
        if (!Regex.IsMatch(duration, "^PT[1-9]\\d*H$", RegexOptions.CultureInvariant))
            Add(findings, "policy.offer-duration", "Offer duration must be a positive whole-hour ISO 8601 duration.");

        return new ValidationGateResult(GateId, "M1 policy invariants v1", findings);
    }

    private static decimal Decimal(JsonElement element, string property) => element.GetProperty(property).GetDecimal();
    private static int Integer(JsonElement element, string property) => element.GetProperty(property).GetInt32();
    private static void Add(ICollection<ValidationFinding> findings, string code, string message)
    {
        if (!findings.Any(finding => finding.Code == code)) findings.Add(new(code, ValidationSeverity.Error, message));
    }
}
