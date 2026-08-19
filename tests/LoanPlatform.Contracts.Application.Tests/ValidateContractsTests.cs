using LoanPlatform.Contracts.Application.Ports;
using LoanPlatform.Contracts.Application.Validation;
using LoanPlatform.Contracts.Domain.Governance;

namespace LoanPlatform.Contracts.Application.Tests;

public sealed class ValidateContractsTests
{
    [Fact]
    public async Task ExecuteReportsInitialBaselineAndExactCounts()
    {
        GovernanceManifest manifest = new(
            Enumerable.Range(1, 16).Select(index =>
                new ContractDescriptor($"Contract{index}", ContractCategory.IntegrationEvent, $"schemas/{index}.json")).ToArray(),
            Enumerable.Range(1, 175).Select(index =>
                new ApprovedFieldPath("Contract1", $"payload.field{index}", DataClassification.Internal, "canonical")).ToArray(),
            "b545d085441ce02a61a400f4eb778673410a366d");
        RecordingWriter writer = new();
        ValidateContracts useCase = new(new StubRepository(manifest), [], new InitialBaseline(), writer, new FixedMetadata());

        ValidationReport report = await useCase.ExecuteAsync(CancellationToken.None);

        Assert.True(report.Succeeded);
        Assert.Equal(16, report.ContractCount);
        Assert.Equal(175, report.FieldPathCount);
        Assert.Equal("Initial baseline — no previous release", report.CompatibilityStatus);
        Assert.Same(report, writer.Report);
    }

    [Fact]
    public async Task AnyMandatoryGateFailureFailsTheConsolidatedReport()
    {
        GovernanceManifest manifest = new(
            Enumerable.Range(1, 16).Select(index => new ContractDescriptor($"Contract{index}", ContractCategory.IntegrationEvent, $"schemas/{index}.json")).ToArray(),
            Enumerable.Range(1, 175).Select(index => new ApprovedFieldPath("Contract1", $"payload.field{index}", DataClassification.Internal, "canonical")).ToArray(),
            "b545d085441ce02a61a400f4eb778673410a366d");
        ValidateContracts useCase = new(new StubRepository(manifest), [new FailingValidator()], new InitialBaseline(), new RecordingWriter(), new FixedMetadata());

        ValidationReport report = await useCase.ExecuteAsync(CancellationToken.None);

        Assert.False(report.Succeeded);
        Assert.Equal("Failed", report.OverallStatus);
        Assert.False(Assert.Single(report.Gates, gate => gate.GateId == "official").Succeeded);
    }

    private sealed class StubRepository(GovernanceManifest manifest) : IArtifactRepository
    {
        public Task<GovernanceManifest> LoadManifestAsync(CancellationToken cancellationToken) => Task.FromResult(manifest);
    }

    private sealed class InitialBaseline : ICompatibilityBaseline
    {
        public Task<string> GetStatusAsync(CancellationToken cancellationToken) =>
            Task.FromResult("Initial baseline — no previous release");
    }

    private sealed class RecordingWriter : IValidationReportWriter
    {
        public ValidationReport? Report { get; private set; }

        public Task WriteAsync(ValidationReport report, CancellationToken cancellationToken)
        {
            Report = report;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedMetadata : IRepositoryMetadata
    {
        public string CommitSha => "feature-sha";
        public DateTimeOffset UtcNow => DateTimeOffset.Parse("2026-08-19T00:00:00Z");
        public string DotNetVersion => "10.0.400";
    }

    private sealed class FailingValidator : IExternalSpecificationValidator
    {
        public string GateId => "official";
        public Task<ValidationGateResult> ValidateAsync(CancellationToken cancellationToken) => Task.FromResult(
            new ValidationGateResult(GateId, "1.0.0", [new ValidationFinding("official.failed", ValidationSeverity.Error, "Expected failure.")]));
    }
}
