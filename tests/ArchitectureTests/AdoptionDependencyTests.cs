using System.Xml.Linq;

namespace ModulithFoundry.ArchitectureTests;

public sealed class AdoptionDependencyTests
{
    [Fact]
    public void ActorHttpAdapterDeclaresOnlyTheCoreAndNativeFramework()
    {
        XDocument declaration = XDocument.Load(
            Path.Combine(
                AppContext.BaseDirectory,
                "ProjectDeclarations",
                "ModulithFoundry.ActorIdentity.AspNetCore.csproj"
            )
        );
        Assert.Empty(declaration.Descendants("PackageReference"));
        Assert.Equal(
            ["Microsoft.AspNetCore.App"],
            declaration
                .Descendants("FrameworkReference")
                .Select(reference => (string)reference.Attribute("Include")!)
        );
        Assert.Equal(
            ["ModulithFoundry.ActorIdentity"],
            declaration
                .Descendants("ProjectReference")
                .Select(reference =>
                    Path.GetFileNameWithoutExtension((string)reference.Attribute("Include")!)
                )
        );
    }

    [Theory]
    [InlineData("ModulithFoundry.ActorIdentity", null)]
    [InlineData("ModulithFoundry.Tenancy", null)]
    [InlineData(
        "ModulithFoundry.Persistence.EntityFrameworkCore",
        "Microsoft.EntityFrameworkCore.Relational"
    )]
    public void RuntimeLibrariesDeclareOnlyTheirIntendedDependencies(
        string library,
        string? package
    )
    {
        XDocument declaration = XDocument.Load(
            Path.Combine(AppContext.BaseDirectory, "ProjectDeclarations", library + ".csproj")
        );
        Assert.Empty(declaration.Descendants("ProjectReference"));
        Assert.Empty(declaration.Descendants("FrameworkReference"));
        string[] packages = declaration
            .Descendants("PackageReference")
            .Select(reference => (string)reference.Attribute("Include")!)
            .ToArray();
        Assert.Equal(package is null ? [] : [package], packages);
    }
}
