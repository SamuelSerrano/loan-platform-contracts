namespace LoanPlatform.Contracts.Domain.Tests;

public sealed class BootstrapTests
{
    [Fact]
    public void DomainTestHarnessIsAvailable()
    {
        Assert.NotNull(typeof(BootstrapTests).Assembly);
    }
}
