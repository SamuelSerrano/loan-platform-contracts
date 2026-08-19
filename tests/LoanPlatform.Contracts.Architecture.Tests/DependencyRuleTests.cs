using System.Xml.Linq;

namespace LoanPlatform.Contracts.Architecture.Tests;

public sealed class DependencyRuleTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Fact]
    public void DomainHasNoProjectOrPackageDependencies() => Assert.Empty(References("Domain"));

    [Fact]
    public void ApplicationDependsOnlyOnDomain() => Assert.Equal(["LoanPlatform.Contracts.Domain"], References("Application"));

    [Fact]
    public void InfrastructureDependsInward() => Assert.Equal(
        ["LoanPlatform.Contracts.Application", "LoanPlatform.Contracts.Domain"], References("Infrastructure").Order());

    [Fact]
    public void CliIsTheCompositionRoot() => Assert.Equal(
        ["LoanPlatform.Contracts.Application", "LoanPlatform.Contracts.Infrastructure"], References("Cli").Order());

    [Fact]
    public void ProductiveProjectsCannotPublishSharedDtoPackages()
    {
        XDocument properties = XDocument.Load(Path.Combine(Root, "Directory.Build.props"));
        Assert.Equal("false", properties.Descendants("IsPackable").Single().Value);
        Assert.DoesNotContain(Directory.EnumerateFiles(Path.Combine(Root, "src"), "*.csproj", SearchOption.AllDirectories),
            path => Path.GetFileName(path).Contains("Dto", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(path).Contains("Models", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> References(string project)
    {
        string path = Path.Combine(Root, "src", $"LoanPlatform.Contracts.{project}", $"LoanPlatform.Contracts.{project}.csproj");
        XDocument document = XDocument.Load(path);
        return document.Descendants("ProjectReference")
            .Select(element => Path.GetFileNameWithoutExtension(element.Attribute("Include")!.Value.Replace('\\', '/')));
    }
}
