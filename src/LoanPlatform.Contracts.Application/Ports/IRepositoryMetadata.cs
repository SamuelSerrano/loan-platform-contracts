namespace LoanPlatform.Contracts.Application.Ports;

public interface IRepositoryMetadata
{
    string CommitSha { get; }

    DateTimeOffset UtcNow { get; }

    string DotNetVersion { get; }
}
