using System.Xml.Linq;

namespace ModulithFoundry.Testing.Architecture;

public static class RepositoryTopology
{
    private const string SolutionFileName = "ModulithFoundry.slnx";

    public static string Root { get; } = FindRoot();

    public static IReadOnlyCollection<string> SolutionProjects()
    {
        XDocument solution = XDocument.Load(Path.Combine(Root, SolutionFileName));

        return solution
            .Descendants("Project")
            .Select(element =>
                Normalize(
                    element.Attribute("Path")?.Value
                        ?? throw new InvalidOperationException("A solution project has no Path.")
                )
            )
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyCollection<ProjectDefinition> Projects()
    {
        return Directory
            .EnumerateFiles(Root, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Select(LoadProject)
            .OrderBy(project => project.Path, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyCollection<ModuleDefinition> Modules()
    {
        ProjectDefinition[] moduleProjects = Projects()
            .Where(project => project.Path.StartsWith("modules/", StringComparison.Ordinal))
            .ToArray();
        var violations = new List<string>();
        var modules = new List<ModuleDefinition>();

        foreach (
            IGrouping<string, ProjectDefinition> group in moduleProjects.GroupBy(
                project => project.Path.Split('/')[1],
                StringComparer.Ordinal
            )
        )
        {
            string moduleName = group.Key;
            string contractsPath =
                $"modules/{moduleName}/{moduleName}.Contracts/{moduleName}.Contracts.csproj";
            string implementationPath = $"modules/{moduleName}/{moduleName}/{moduleName}.csproj";
            ProjectDefinition? contracts = group.SingleOrDefault(project =>
                project.Path == contractsPath
            );
            ProjectDefinition? implementation = group.SingleOrDefault(project =>
                project.Path == implementationPath
            );

            foreach (
                ProjectDefinition unexpected in group.Where(project =>
                    project.Path != contractsPath && project.Path != implementationPath
                )
            )
            {
                violations.Add(
                    $"Module {moduleName} contains unexpected project {unexpected.Path}"
                );
            }

            if (contracts is null)
            {
                violations.Add($"Module {moduleName} is missing Contracts project {contractsPath}");
            }

            if (implementation is null)
            {
                violations.Add(
                    $"Module {moduleName} is missing implementation project {implementationPath}"
                );
            }

            if (contracts is null || implementation is null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(implementation.ModuleSchema))
            {
                violations.Add($"Module {moduleName} implementation does not declare ModuleSchema");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(contracts.ModuleSchema))
            {
                violations.Add(
                    $"Module {moduleName} Contracts project must not declare ModuleSchema"
                );
                continue;
            }

            modules.Add(
                new ModuleDefinition(
                    moduleName,
                    implementation.ModuleSchema,
                    contracts,
                    implementation
                )
            );
        }

        if (violations.Count > 0)
        {
            throw new InvalidOperationException(
                $"Repository module topology is invalid:{Environment.NewLine}- "
                    + string.Join($"{Environment.NewLine}- ", violations)
            );
        }

        return modules.OrderBy(module => module.Name, StringComparer.Ordinal).ToArray();
    }

    private static ProjectDefinition LoadProject(string fullPath)
    {
        XDocument document = XDocument.Load(fullPath);
        string relativePath = Normalize(Path.GetRelativePath(Root, fullPath));
        string projectDirectory =
            Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException($"Project '{relativePath}' has no directory.");

        string[] projectReferences = document
            .Descendants("ProjectReference")
            .Select(element =>
                element.Attribute("Include")?.Value
                ?? throw new InvalidOperationException(
                    $"A reference in '{relativePath}' has no Include."
                )
            )
            .Select(reference => Path.GetFullPath(reference, projectDirectory))
            .Select(reference => Normalize(Path.GetRelativePath(Root, reference)))
            .Order(StringComparer.Ordinal)
            .ToArray();

        string[] packageReferences = document
            .Descendants("PackageReference")
            .Select(element =>
                element.Attribute("Include")?.Value
                ?? throw new InvalidOperationException(
                    $"A package reference in '{relativePath}' has no Include."
                )
            )
            .Order(StringComparer.Ordinal)
            .ToArray();

        string[] frameworkReferences = document
            .Descendants("FrameworkReference")
            .Select(element =>
                element.Attribute("Include")?.Value
                ?? throw new InvalidOperationException(
                    $"A framework reference in '{relativePath}' has no Include."
                )
            )
            .Order(StringComparer.Ordinal)
            .ToArray();

        string sdk =
            document.Root?.Attribute("Sdk")?.Value
            ?? throw new InvalidOperationException($"Project '{relativePath}' has no SDK.");
        string assemblyName =
            PropertyValue(document, "AssemblyName") ?? Path.GetFileNameWithoutExtension(fullPath);

        return new ProjectDefinition(
            relativePath,
            sdk,
            assemblyName,
            PropertyValue(document, "ModuleSchema"),
            projectReferences,
            packageReferences,
            frameworkReferences
        );
    }

    private static string? PropertyValue(XContainer document, string name) =>
        document
            .Descendants(name)
            .Select(element => element.Value)
            .LastOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static bool IsBuildOutput(string path)
    {
        string relativePath = Normalize(Path.GetRelativePath(Root, path));
        return relativePath.Contains("/bin/", StringComparison.Ordinal)
            || relativePath.Contains("/obj/", StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not find {SolutionFileName} above the test output directory."
        );
    }

    private static string Normalize(string path) => path.Replace('\\', '/');
}

public sealed record ProjectDefinition(
    string Path,
    string Sdk,
    string AssemblyName,
    string? ModuleSchema,
    IReadOnlyCollection<string> ProjectReferences,
    IReadOnlyCollection<string> PackageReferences,
    IReadOnlyCollection<string> FrameworkReferences
);

public sealed record ModuleDefinition(
    string Name,
    string Schema,
    ProjectDefinition ContractsProject,
    ProjectDefinition ImplementationProject
);
