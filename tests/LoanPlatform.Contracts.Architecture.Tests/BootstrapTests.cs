namespace LoanPlatform.Contracts.Architecture.Tests;

public sealed class BootstrapTests
{
    [Fact]
    public void ArchitectureTestHarnessIsAvailable()
    {
        Assert.NotNull(typeof(BootstrapTests).Assembly);
    }
}
