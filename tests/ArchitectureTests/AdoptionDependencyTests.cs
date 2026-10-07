using System.Xml.Linq;

namespace ModulithFoundry.ArchitectureTests;

public sealed class AdoptionDependencyTests
{
    [Theory]
    [InlineData("ModulithFoundry.ActorIdentity.AspNetCore", "ModulithFoundry.ActorIdentity")]
    [InlineData("ModulithFoundry.Tenancy.AspNetCore", "ModulithFoundry.Tenancy")]
    public void HttpAdaptersDeclareOnlyTheirCoreAndNativeFramework(string adapter, string core)
    {
        XDocument declaration = XDocument.Load(
            Path.Combine(AppContext.BaseDirectory, "ProjectDeclarations", adapter + ".csproj")
        );
        Assert.Empty(declaration.Descendants("PackageReference"));
        Assert.Equal(
            ["Microsoft.AspNetCore.App"],
            declaration
                .Descendants("FrameworkReference")
                .Select(reference => (string)reference.Attribute("Include")!)
        );
        Assert.Equal(
            [core],
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
    [InlineData("ModulithFoundry.Events.Serialization", null)]
    [InlineData("ModulithFoundry.Events.History", null)]
    [InlineData(
        "ModulithFoundry.Persistence.EntityFrameworkCore",
        "Microsoft.EntityFrameworkCore.Relational"
    )]
    [InlineData("ModulithFoundry.EventSourcing", null)]
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

    [Fact]
    public void EventAdapterDeclaresAggregateCoreHistoryIntegrityNativeEfAndDiAbstractions()
    {
        XDocument declaration = XDocument.Load(
            Path.Combine(
                AppContext.BaseDirectory,
                "ProjectDeclarations",
                "ModulithFoundry.EventSourcing.EntityFrameworkCore.csproj"
            )
        );
        Assert.Equal(
            ["ModulithFoundry.EventSourcing", "ModulithFoundry.Events.History"],
            declaration
                .Descendants("ProjectReference")
                .Select(reference =>
                    Path.GetFileNameWithoutExtension((string)reference.Attribute("Include")!)
                )
        );
        Assert.Equal(
            [
                "Microsoft.EntityFrameworkCore.Relational",
                "Microsoft.Extensions.DependencyInjection.Abstractions",
            ],
            declaration
                .Descendants("PackageReference")
                .Select(reference => (string)reference.Attribute("Include")!)
        );
        Assert.Empty(declaration.Descendants("FrameworkReference"));
    }

    [Fact]
    public void IndependentStorageConsumerDeclaresOnlyStorageAndNativeProviderPackages()
    {
        XDocument declaration = XDocument.Load(
            Path.Combine(AppContext.BaseDirectory, "ProjectDeclarations", "EventStorageDemo.csproj")
        );
        Assert.Equal(
            ["ModulithFoundry.EventSourcing", "ModulithFoundry.EventSourcing.EntityFrameworkCore"],
            declaration
                .Descendants("ProjectReference")
                .Select(reference =>
                    Path.GetFileNameWithoutExtension((string)reference.Attribute("Include")!)
                )
        );
        Assert.Equal(
            ["Microsoft.EntityFrameworkCore.Design", "Npgsql.EntityFrameworkCore.PostgreSQL"],
            declaration
                .Descendants("PackageReference")
                .Select(reference => (string)reference.Attribute("Include")!)
                .Order(StringComparer.Ordinal)
        );
        Assert.Empty(declaration.Descendants("FrameworkReference"));
    }

    [Fact]
    public void StandalonePostgresConsumerDeclaresOnlyItsSelectedDependencies()
    {
        var consumer = XDocument.Load(
            Path.Combine(
                AppContext.BaseDirectory,
                "ProjectDeclarations",
                "EventSourcingPostgresTests.csproj"
            )
        );
        Assert.Equal(
            ["ModulithFoundry.EventSourcing.EntityFrameworkCore"],
            consumer
                .Descendants("ProjectReference")
                .Select(reference =>
                    Path.GetFileNameWithoutExtension((string)reference.Attribute("Include")!)
                )
        );
        Assert.Equal(
            [
                "Npgsql.EntityFrameworkCore.PostgreSQL",
                "Testcontainers.PostgreSql",
                "xunit.v3.mtp-v2",
            ],
            consumer
                .Descendants("PackageReference")
                .Select(reference => (string)reference.Attribute("Include")!)
                .Order(StringComparer.Ordinal)
        );
    }
}
