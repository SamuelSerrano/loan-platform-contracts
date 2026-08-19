using LoanPlatform.Contracts.Domain.Governance;

namespace LoanPlatform.Contracts.Domain.Tests;

public sealed class ContractInventoryTests
{
    [Fact]
    public void ValidateAcceptsExactInitialBoundary()
    {
        ContractDescriptor[] contracts = Enumerable.Range(1, 16)
            .Select(index => new ContractDescriptor($"Contract{index}", ContractCategory.IntegrationEvent, "schema.json"))
            .ToArray();
        ApprovedFieldPath[] fields = Enumerable.Range(1, 175)
            .Select(index => new ApprovedFieldPath("Contract1", $"payload.field{index}", DataClassification.Internal, "canonical"))
            .ToArray();

        IReadOnlyList<ValidationFinding> findings = ContractInventory.Validate(contracts, fields);

        Assert.Empty(findings);
    }

    [Fact]
    public void ValidateRejectsDuplicateAndUnexpectedCounts()
    {
        ContractDescriptor contract = new("Duplicate", ContractCategory.HttpOperation, "schema.json");
        ApprovedFieldPath field = new("Duplicate", "request.value", DataClassification.Public, "canonical");

        IReadOnlyList<ValidationFinding> findings = ContractInventory.Validate([contract, contract], [field, field]);

        Assert.Contains(findings, finding => finding.Code == "inventory.contract-count");
        Assert.Contains(findings, finding => finding.Code == "inventory.contract-duplicate");
        Assert.Contains(findings, finding => finding.Code == "inventory.field-count");
        Assert.Contains(findings, finding => finding.Code == "inventory.field-duplicate");
    }

    [Fact]
    public void VersionRequiresPositiveVisibleMajor()
    {
        Assert.True(ContractVersion.TryCreate(1, out ContractVersion version));
        Assert.Equal(1, version.Major);
        Assert.False(ContractVersion.TryCreate(0, out _));
    }
}
