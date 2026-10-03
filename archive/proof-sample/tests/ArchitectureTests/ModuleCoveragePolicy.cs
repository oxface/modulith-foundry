namespace ArchitectureTests;

internal static class ModuleCoveragePolicy
{
    internal static IReadOnlyCollection<string> AdapterViolations(
        IEnumerable<string> discoveredModules,
        IEnumerable<string> adaptedModules
    )
    {
        var adapted = new HashSet<string>(adaptedModules, StringComparer.Ordinal);
        var discovered = new HashSet<string>(discoveredModules, StringComparer.Ordinal);

        return discovered
            .Where(module => !adapted.Contains(module))
            .Order(StringComparer.Ordinal)
            .Select(module => $"{module} has no persistence architecture-test adapter")
            .Concat(
                adapted
                    .Where(module => !discovered.Contains(module))
                    .Order(StringComparer.Ordinal)
                    .Select(module =>
                        $"{module} persistence architecture-test adapter has no discovered module"
                    )
            )
            .ToArray();
    }
}
