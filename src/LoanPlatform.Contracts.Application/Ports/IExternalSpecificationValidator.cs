using LoanPlatform.Contracts.Domain.Governance;

namespace LoanPlatform.Contracts.Application.Ports;

public interface IExternalSpecificationValidator
{
    string GateId { get; }

    Task<IReadOnlyList<ValidationFinding>> ValidateAsync(CancellationToken cancellationToken);
}
