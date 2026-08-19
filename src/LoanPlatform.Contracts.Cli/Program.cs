using LoanPlatform.Contracts.Application.Validation;
using LoanPlatform.Contracts.Infrastructure.Artifacts;
using LoanPlatform.Contracts.Infrastructure.Reporting;
using LoanPlatform.Contracts.Infrastructure.Repository;
using LoanPlatform.Contracts.Infrastructure.Validation;

if (args is not ["validate"])
{
    Console.Error.WriteLine("Usage: loan-platform-contracts validate");
    return 2;
}

string root = FindRepositoryRoot(Environment.CurrentDirectory);
ValidateContracts useCase = new(
    new YamlArtifactRepository(root),
    [new ArtifactStructureValidator(root)],
    new InitialCompatibilityBaseline(),
    new JsonValidationReportWriter(root),
    new LocalRepositoryMetadata(root));
ValidationReport report = await useCase.ExecuteAsync(CancellationToken.None);
Console.WriteLine($"Validated {report.ContractCount} contracts and {report.FieldPathCount} approved field paths; {report.Findings.Count} finding(s).");
return report.Succeeded ? 0 : 1;

static string FindRepositoryRoot(string start)
{
    DirectoryInfo? directory = new(start);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LoanPlatform.Contracts.slnx")))
        directory = directory.Parent;
    return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
}
