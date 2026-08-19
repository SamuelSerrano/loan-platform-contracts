namespace LoanPlatform.Contracts.Domain.Governance;

public sealed record ApprovedFieldPath(
    string Contract,
    string Path,
    DataClassification Classification,
    string CanonicalSource);
