namespace LoanPlatform.Contracts.Infrastructure.Tests;

public sealed class BootstrapTests
{
    [Fact]
    public void InfrastructureTestHarnessIsAvailable()
    {
        Assert.NotNull(typeof(BootstrapTests).Assembly);
    }
}
