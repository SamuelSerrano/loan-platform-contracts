namespace LoanPlatform.Contracts.Domain.Governance;

public sealed record ValidationFinding(string Code, ValidationSeverity Severity, string Message);

public enum ValidationSeverity
{
    Information,
    Warning,
    Error
}
