namespace ModulithFoundry.PersistenceTests;

internal static class MigrationCoveragePolicy
{
    internal static IReadOnlyCollection<string> SchemaViolations(
        IEnumerable<string> expectedSchemas,
        IEnumerable<string> migratedSchemas)
    {
        var expected = new HashSet<string>(expectedSchemas, StringComparer.Ordinal);
        var migrated = new HashSet<string>(migratedSchemas, StringComparer.Ordinal);

        return expected
            .Where(schema => !migrated.Contains(schema))
            .Order(StringComparer.Ordinal)
            .Select(schema => $"Module schema '{schema}' was not migrated")
            .Concat(migrated
                .Where(schema => !expected.Contains(schema))
                .Order(StringComparer.Ordinal)
                .Select(schema => $"Unexpected module schema '{schema}' was migrated"))
            .ToArray();
    }
}
