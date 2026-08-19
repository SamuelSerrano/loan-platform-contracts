using LoanPlatform.Contracts.Application.Ports;
using LoanPlatform.Contracts.Domain.Governance;

namespace LoanPlatform.Contracts.Infrastructure.Validation;

public sealed class PublicBoundaryValidator(string repositoryRoot) : IExternalSpecificationValidator
{
    private static readonly string[] Prohibited = ["reasonCodes", "reasonCode", "rawToken", "jwtPayload", "rulesEvaluated", "internalScore"];
    public string GateId => "public-boundary-q008";

    public Task<ValidationGateResult> ValidateAsync(CancellationToken cancellationToken)
    {
        IEnumerable<string> files = Directory.EnumerateFiles(Path.Combine(repositoryRoot, "openapi"), "*", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(repositoryRoot, "schemas", "common"), "*.json"))
            .Concat(Directory.EnumerateFiles(Path.Combine(repositoryRoot, "examples", "positive"), "*.json")
                .Where(path => Path.GetFileName(path) is "start-credit-application.json" or "submit-credit-application.json" or "get-credit-application-status.json" or "accept-credit-offer.json" or "standard-problem.json"));
        List<ValidationFinding> findings = [];
        foreach (string file in files)
        {
            string text = File.ReadAllText(file);
            foreach (string prohibited in Prohibited.Where(value => text.Contains(value, StringComparison.OrdinalIgnoreCase)))
                findings.Add(new("security.public-prohibited", ValidationSeverity.Error,
                    $"{Path.GetRelativePath(repositoryRoot, file)} exposes prohibited public token {prohibited}."));
        }
        return Task.FromResult(new ValidationGateResult(GateId, "built-in/Q-008", findings));
    }
}
