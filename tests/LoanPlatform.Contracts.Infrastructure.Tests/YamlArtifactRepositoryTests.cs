using LoanPlatform.Contracts.Infrastructure.Artifacts;

namespace LoanPlatform.Contracts.Infrastructure.Tests;

public sealed class YamlArtifactRepositoryTests
{
    [Fact]
    public async Task LoadsExactApprovedBoundary()
    {
        var manifest = await new YamlArtifactRepository(RepositoryRoot()).LoadManifestAsync(CancellationToken.None);

        Assert.Equal(16, manifest.Contracts.Count);
        Assert.Equal(175, manifest.FieldPaths.Count);
        Assert.Equal("b545d085441ce02a61a400f4eb778673410a366d", manifest.ArchitectureSourceCommit);
    }

    private static string RepositoryRoot() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
}
