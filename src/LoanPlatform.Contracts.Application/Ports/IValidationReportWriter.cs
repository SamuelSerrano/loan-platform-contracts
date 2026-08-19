using LoanPlatform.Contracts.Application.Validation;

namespace LoanPlatform.Contracts.Application.Ports;

public interface IValidationReportWriter
{
    Task WriteAsync(ValidationReport report, CancellationToken cancellationToken);
}
