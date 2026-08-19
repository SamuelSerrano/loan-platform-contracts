using System.Text.Json;
using LoanPlatform.Contracts.Application.Ports;
using LoanPlatform.Contracts.Application.Validation;

namespace LoanPlatform.Contracts.Infrastructure.Reporting;

public sealed class JsonValidationReportWriter(string repositoryRoot) : IValidationReportWriter
{
    public async Task WriteAsync(ValidationReport report, CancellationToken cancellationToken)
    {
        string directory = Path.Combine(repositoryRoot, "artifacts", "validation");
        Directory.CreateDirectory(directory);
        await using FileStream stream = File.Create(Path.Combine(directory, "validation-report.json"));
        await JsonSerializer.SerializeAsync(stream, report, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
    }
}
