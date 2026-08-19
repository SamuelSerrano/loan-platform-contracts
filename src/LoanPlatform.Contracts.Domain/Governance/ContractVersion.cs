namespace LoanPlatform.Contracts.Domain.Governance;

public readonly record struct ContractVersion
{
    private ContractVersion(int major) => Major = major;

    public int Major { get; }

    public static bool TryCreate(int major, out ContractVersion version)
    {
        version = major > 0 ? new ContractVersion(major) : default;
        return major > 0;
    }
}
