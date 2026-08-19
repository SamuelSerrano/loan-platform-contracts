namespace LoanPlatform.Contracts.ContractTests;

public sealed class BootstrapTests
{
    [Fact]
    public void ContractTestHarnessIsAvailable()
    {
        Assert.NotNull(typeof(BootstrapTests).Assembly);
    }
}
