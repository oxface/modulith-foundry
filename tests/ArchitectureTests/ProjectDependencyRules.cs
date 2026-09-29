using ModulithFoundry.Testing.Architecture;

namespace ArchitectureTests;

internal static class ProjectDependencyRules
{
    private const string ApiProject = "apps/Api/ModulithFoundry.Api.csproj";
    private const string AppHostProject = "apps/AppHost/ModulithFoundry.AppHost.csproj";
    private const string MigratorProject = "apps/Migrator/ModulithFoundry.Migrator.csproj";
    private const string TopologyTestsProject = "tests/TopologyTests/TopologyTests.csproj";

    // Contracts-to-Contracts edges are exceptional and enter this allowlist with the real contract that needs them.
    private static readonly HashSet<string> AllowedContractReferences = new(
        StringComparer.Ordinal)
    {
        "modules/Inventory/Inventory.Contracts/Inventory.Contracts.csproj -> modules/Access/Access.Contracts/Access.Contracts.csproj",
        "modules/Purchasing/Purchasing.Contracts/Purchasing.Contracts.csproj -> modules/Access/Access.Contracts/Access.Contracts.csproj",
        "modules/Sales/Sales.Contracts/Sales.Contracts.csproj -> modules/Access/Access.Contracts/Access.Contracts.csproj",
    };

    internal static IReadOnlyCollection<string> ReferenceViolations(
        IReadOnlyCollection<ProjectDefinition> projects)
    {
        Dictionary<string, ProjectDefinition> projectsByPath = projects.ToDictionary(
            project => project.Path,
            StringComparer.Ordinal);
        List<string> violations = [];

        foreach (ProjectDefinition project in projects)
        {
            foreach (string reference in project.ProjectReferences)
            {
                if (!projectsByPath.ContainsKey(reference))
                {
                    violations.Add($"{project.Path} references missing project {reference}");
                    continue;
                }

                if (!IsAllowedReference(project.Path, reference))
                {
                    violations.Add($"{project.Path} has forbidden reference {reference}");
                }
            }

            AddRequiredReferenceViolations(project, projects, violations);
        }

        return violations;
    }

    internal static IReadOnlyCollection<string> Cycles(
        IReadOnlyDictionary<string, IReadOnlySet<string>> references)
    {
        List<string> cycles = [];
        HashSet<string> visited = new(StringComparer.Ordinal);
        HashSet<string> active = new(StringComparer.Ordinal);
        Stack<string> path = new();

        foreach (string project in references.Keys)
        {
            Visit(project, references, visited, active, path, cycles);
        }

        return cycles;
    }

    internal static IReadOnlyCollection<string> AspirePackageReferenceViolations(
        IReadOnlyCollection<ProjectDefinition> projects)
    {
        return projects
            .Where(project => project.Path != AppHostProject
                && project.Path != TopologyTestsProject)
            .SelectMany(project => project.PackageReferences
                .Where(package => package.StartsWith("Aspire.", StringComparison.Ordinal))
                .Select(package =>
                    $"{project.Path} has forbidden Aspire package reference {package}"))
            .ToArray();
    }

    private static void AddRequiredReferenceViolations(
        ProjectDefinition project,
        IReadOnlyCollection<ProjectDefinition> projects,
        ICollection<string> violations)
    {
        if (IsModuleImplementation(project.Path))
        {
            AddMissingReferenceViolation(project, OwnContractsProject(project.Path), violations);
        }

        if (project.Path == ApiProject)
        {
            foreach (string implementation in projects
                .Select(candidate => candidate.Path)
                .Where(IsModuleImplementation))
            {
                AddMissingReferenceViolation(project, implementation, violations);
            }
        }
    }

    private static void AddMissingReferenceViolation(
        ProjectDefinition project,
        string requiredReference,
        ICollection<string> violations)
    {
        if (!project.ProjectReferences.Contains(requiredReference, StringComparer.Ordinal))
        {
            violations.Add($"{project.Path} is missing required reference {requiredReference}");
        }
    }

    private static bool IsAllowedReference(string source, string target)
    {
        if (IsTestProject(source))
        {
            return true;
        }

        if (IsModuleImplementation(source))
        {
            return IsContractsProject(target) || IsSharedProject(target);
        }

        if (IsContractsProject(source))
        {
            return IsContractsProject(target)
                && AllowedContractReferences.Contains(ContractReferenceKey(source, target));
        }

        if (source == ApiProject || source == MigratorProject)
        {
            return IsModuleImplementation(target) || IsSharedProject(target);
        }

        if (source == AppHostProject)
        {
            return IsRunnableApplication(target);
        }

        if (IsSharedProject(source))
        {
            return IsSharedProject(target);
        }

        return false;
    }

    private static string OwnContractsProject(string implementationProject)
    {
        string[] segments = implementationProject.Split('/');
        string moduleName = segments[1];
        return $"modules/{moduleName}/{moduleName}.Contracts/{moduleName}.Contracts.csproj";
    }

    private static bool IsContractsProject(string path) =>
        path.StartsWith("modules/", StringComparison.Ordinal)
        && path.Contains(".Contracts/", StringComparison.Ordinal);

    private static bool IsModuleImplementation(string path) =>
        path.StartsWith("modules/", StringComparison.Ordinal)
        && !IsContractsProject(path);

    private static bool IsRunnableApplication(string path) =>
        path.StartsWith("apps/", StringComparison.Ordinal)
        && path != AppHostProject;

    private static bool IsSharedProject(string path) =>
        path.StartsWith("shared/", StringComparison.Ordinal);

    private static bool IsTestProject(string path) =>
        path.StartsWith("tests/", StringComparison.Ordinal);

    private static string ContractReferenceKey(string source, string target) => $"{source} -> {target}";

    private static void Visit(
        string project,
        IReadOnlyDictionary<string, IReadOnlySet<string>> references,
        ISet<string> visited,
        ISet<string> active,
        Stack<string> path,
        ICollection<string> cycles)
    {
        if (active.Contains(project))
        {
            string[] route = path.Reverse().SkipWhile(item => item != project).Append(project).ToArray();
            cycles.Add(string.Join(" -> ", route));
            return;
        }

        if (!visited.Add(project))
        {
            return;
        }

        active.Add(project);
        path.Push(project);

        if (references.TryGetValue(project, out IReadOnlySet<string>? dependencies))
        {
            foreach (string dependency in dependencies)
            {
                Visit(dependency, references, visited, active, path, cycles);
            }
        }

        path.Pop();
        active.Remove(project);
    }
}
