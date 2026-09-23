using System.Xml.Linq;

namespace ArchitectureTests;

internal static class RepositoryLayout
{
    private const string SolutionFileName = "ModulithFoundry.slnx";

    internal static string Root { get; } = FindRoot();

    internal static IReadOnlyCollection<string> SolutionProjects()
    {
        XDocument solution = XDocument.Load(Path.Combine(Root, SolutionFileName));

        return solution
            .Descendants("Project")
            .Select(element => Normalize(element.Attribute("Path")?.Value
                ?? throw new InvalidOperationException("A solution project has no Path.")))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    internal static IReadOnlyCollection<ProjectDefinition> RepositoryProjects()
    {
        return Directory
            .EnumerateFiles(Root, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Select(LoadProject)
            .OrderBy(project => project.Path, StringComparer.Ordinal)
            .ToArray();
    }

    private static ProjectDefinition LoadProject(string fullPath)
    {
        XDocument document = XDocument.Load(fullPath);
        string relativePath = Normalize(Path.GetRelativePath(Root, fullPath));
        string projectDirectory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException($"Project '{relativePath}' has no directory.");

        string[] projectReferences = document
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value
                ?? throw new InvalidOperationException($"A reference in '{relativePath}' has no Include."))
            .Select(reference => Path.GetFullPath(reference, projectDirectory))
            .Select(reference => Normalize(Path.GetRelativePath(Root, reference)))
            .Order(StringComparer.Ordinal)
            .ToArray();

        string[] packageReferences = document
            .Descendants("PackageReference")
            .Select(element => element.Attribute("Include")?.Value
                ?? throw new InvalidOperationException($"A package reference in '{relativePath}' has no Include."))
            .Order(StringComparer.Ordinal)
            .ToArray();

        string[] frameworkReferences = document
            .Descendants("FrameworkReference")
            .Select(element => element.Attribute("Include")?.Value
                ?? throw new InvalidOperationException($"A framework reference in '{relativePath}' has no Include."))
            .Order(StringComparer.Ordinal)
            .ToArray();

        string sdk = document.Root?.Attribute("Sdk")?.Value
            ?? throw new InvalidOperationException($"Project '{relativePath}' has no SDK.");

        return new ProjectDefinition(
            relativePath,
            sdk,
            projectReferences,
            packageReferences,
            frameworkReferences);
    }

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

        throw new InvalidOperationException($"Could not find {SolutionFileName} above the test output directory.");
    }

    private static string Normalize(string path) => path.Replace('\\', '/');
}

internal sealed record ProjectDefinition(
    string Path,
    string Sdk,
    IReadOnlyCollection<string> ProjectReferences,
    IReadOnlyCollection<string> PackageReferences,
    IReadOnlyCollection<string> FrameworkReferences);
