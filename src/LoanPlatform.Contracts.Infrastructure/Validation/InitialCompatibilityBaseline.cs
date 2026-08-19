using LoanPlatform.Contracts.Application.Ports;

namespace LoanPlatform.Contracts.Infrastructure.Validation;

public sealed class InitialCompatibilityBaseline : ICompatibilityBaseline
{
    public Task<string> GetStatusAsync(CancellationToken cancellationToken) =>
        Task.FromResult("Initial baseline — no previous release");
}
