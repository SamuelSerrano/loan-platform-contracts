namespace LoanPlatform.Contracts.Domain.Governance;

public sealed record CompatibilityChange(string Path, CompatibilityImpact Impact, string Description);

public enum CompatibilityImpact
{
    Compatible,
    Breaking
}
