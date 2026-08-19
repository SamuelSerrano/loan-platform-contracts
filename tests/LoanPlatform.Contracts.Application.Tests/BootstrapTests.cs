namespace LoanPlatform.Contracts.Application.Tests;

public sealed class BootstrapTests
{
    [Fact]
    public void ApplicationTestHarnessIsAvailable()
    {
        Assert.NotNull(typeof(BootstrapTests).Assembly);
    }
}
