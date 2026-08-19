using LoanPlatform.Contracts.Application.Ports;
using LoanPlatform.Contracts.Domain.Governance;

namespace LoanPlatform.Contracts.Application.Validation;

public sealed record ValidationReport(
    string CommitSha,
    DateTimeOffset GeneratedAtUtc,
    string DotNetVersion,
    string ArchitectureSourceCommit,
    string OpenApiVersion,
    string AsyncApiVersion,
    string JsonSchemaDialect,
    int ContractCount,
    int FieldPathCount,
    string CompatibilityStatus,
    IReadOnlyList<ValidationGateResult> Gates,
    IReadOnlyList<ValidationFinding> Findings)
{
    public bool Succeeded => Findings.All(finding => finding.Severity != ValidationSeverity.Error);
    public string OverallStatus => Succeeded ? "Passed" : "Failed";
}
