namespace LoanPlatform.Contracts.Application.Ports;

public interface ICompatibilityBaseline
{
    Task<string> GetStatusAsync(CancellationToken cancellationToken);
}
