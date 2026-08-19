namespace LoanPlatform.Contracts.Domain.Governance;

public static class ContractInventory
{
    public const int ExpectedContractCount = 16;
    public const int ExpectedFieldPathCount = 175;

    public static IReadOnlyList<ValidationFinding> Validate(
        IReadOnlyCollection<ContractDescriptor> contracts,
        IReadOnlyCollection<ApprovedFieldPath> fields)
    {
        List<ValidationFinding> findings = [];
        AddCountFinding(findings, "inventory.contract-count", ExpectedContractCount, contracts.Count, "contracts");
        AddCountFinding(findings, "inventory.field-count", ExpectedFieldPathCount, fields.Count, "approved field paths");

        AddDuplicates(findings, "inventory.contract-duplicate", contracts.Select(contract => contract.Name));
        AddDuplicates(findings, "inventory.field-duplicate", fields.Select(field => $"{field.Contract}:{field.Path}"));
        return findings;
    }

    private static void AddCountFinding(
        ICollection<ValidationFinding> findings,
        string code,
        int expected,
        int actual,
        string noun)
    {
        if (actual != expected)
        {
            findings.Add(new ValidationFinding(code, ValidationSeverity.Error,
                $"Expected exactly {expected} {noun}; found {actual}."));
        }
    }

    private static void AddDuplicates(
        ICollection<ValidationFinding> findings,
        string code,
        IEnumerable<string> values)
    {
        foreach (string duplicate in values.GroupBy(value => value, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1)
                     .Select(group => group.Key)
                     .Order(StringComparer.Ordinal))
        {
            findings.Add(new ValidationFinding(code, ValidationSeverity.Error, $"Duplicate entry: {duplicate}."));
        }
    }
}
