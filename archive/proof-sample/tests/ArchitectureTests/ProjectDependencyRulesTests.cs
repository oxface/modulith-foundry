using ModulithFoundry.Testing.Architecture;

namespace ArchitectureTests;

public sealed class ProjectDependencyRulesTests
{
    [Fact]
    public void Solution_WhenProjectsAreDiscovered_ContainsEveryRepositoryProject()
    {
        string[] expected =
        [
            .. RepositoryTopology
                .Projects()
                .Select(project => project.Path)
                .Order(StringComparer.Ordinal),
        ];
        string[] actual = [.. RepositoryTopology.SolutionProjects()];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ProjectReferences_WhenInspected_RespectDependencyRules()
    {
        ProjectDefinition[] projects = [.. RepositoryTopology.Projects()];

        Assert.Empty(ProjectDependencyRules.ReferenceViolations(projects));
    }

    [Fact]
    public void ProjectDependencyGraph_IsAcyclic()
    {
        Assert.Empty(ProjectDependencyRules.Cycles(LoadReferences()));
    }

    [Fact]
    public void ContractsProjects_WhenInspected_HaveNoInfrastructureDependencies()
    {
        ProjectDefinition[] contracts = RepositoryTopology
            .Projects()
            .Where(project => project.Path.Contains(".Contracts/", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(contracts);

        foreach (ProjectDefinition project in contracts)
        {
            Assert.Equal("Microsoft.NET.Sdk", project.Sdk);
            Assert.Empty(project.PackageReferences);
            Assert.Empty(project.FrameworkReferences);
        }
    }

    [Fact]
    public void PackageReferences_WhenInspected_KeepAspireInLocalOrchestrationProjects()
    {
        ProjectDefinition[] projects = [.. RepositoryTopology.Projects()];

        Assert.Empty(ProjectDependencyRules.AspirePackageReferenceViolations(projects));
    }

    [Fact]
    public void PackageReferences_WhenRuntimeProjectReferencesAspire_ReportViolation()
    {
        const string apiProject = "apps/Api/ModulithFoundry.Api.csproj";
        ProjectDefinition[] invalidProjects = RepositoryTopology
            .Projects()
            .Select(project =>
                project.Path == apiProject
                    ? project with
                    {
                        PackageReferences = project
                            .PackageReferences.Append("Aspire.Npgsql")
                            .ToArray(),
                    }
                    : project
            )
            .ToArray();

        IReadOnlyCollection<string> violations =
            ProjectDependencyRules.AspirePackageReferenceViolations(invalidProjects);

        Assert.Contains(
            $"{apiProject} has forbidden Aspire package reference Aspire.Npgsql",
            violations
        );
    }

    [Fact]
    public void ProjectReferences_WhenModuleReferencesAnotherImplementation_ReportViolation()
    {
        const string salesProject = "modules/Sales/Sales/Sales.csproj";
        const string inventoryProject = "modules/Inventory/Inventory/Inventory.csproj";
        ProjectDefinition[] invalidProjects = RepositoryTopology
            .Projects()
            .Select(project =>
                project.Path == salesProject
                    ? project with
                    {
                        ProjectReferences = project
                            .ProjectReferences.Append(inventoryProject)
                            .ToArray(),
                    }
                    : project
            )
            .ToArray();

        IReadOnlyCollection<string> violations = ProjectDependencyRules.ReferenceViolations(
            invalidProjects
        );

        Assert.Contains($"{salesProject} has forbidden reference {inventoryProject}", violations);
    }

    private static Dictionary<string, IReadOnlySet<string>> LoadReferences()
    {
        return RepositoryTopology
            .Projects()
            .ToDictionary(
                project => project.Path,
                project =>
                    (IReadOnlySet<string>)
                        new HashSet<string>(project.ProjectReferences, StringComparer.Ordinal),
                StringComparer.Ordinal
            );
    }
}
