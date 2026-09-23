namespace ArchitectureTests;

public sealed class ProjectDependencyRulesTests
{
    [Fact]
    public void Solution_WhenProjectsAreDiscovered_ContainsEveryRepositoryProject()
    {
        string[] expected = [.. RepositoryLayout.RepositoryProjects()
            .Select(project => project.Path)
            .Order(StringComparer.Ordinal)];
        string[] actual = [.. RepositoryLayout.SolutionProjects()];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ProjectReferences_WhenInspected_RespectDependencyRules()
    {
        ProjectDefinition[] projects = [.. RepositoryLayout.RepositoryProjects()];

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
        ProjectDefinition[] contracts = RepositoryLayout.RepositoryProjects()
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
    public void ProjectReferences_WhenModuleReferencesAnotherImplementation_ReportViolation()
    {
        const string salesProject = "modules/Sales/Sales/Sales.csproj";
        const string inventoryProject = "modules/Inventory/Inventory/Inventory.csproj";
        ProjectDefinition[] invalidProjects = RepositoryLayout.RepositoryProjects()
            .Select(project => project.Path == salesProject
                ? project with
                {
                    ProjectReferences = project.ProjectReferences.Append(inventoryProject).ToArray()
                }
                : project)
            .ToArray();

        IReadOnlyCollection<string> violations = ProjectDependencyRules.ReferenceViolations(invalidProjects);

        Assert.Contains($"{salesProject} has forbidden reference {inventoryProject}", violations);
    }

    private static Dictionary<string, IReadOnlySet<string>> LoadReferences()
    {
        return RepositoryLayout.RepositoryProjects().ToDictionary(
            project => project.Path,
            project => (IReadOnlySet<string>)new HashSet<string>(
                project.ProjectReferences,
                StringComparer.Ordinal),
            StringComparer.Ordinal);
    }
}
