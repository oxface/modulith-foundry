using System.Collections;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using ModulithFoundry.Modules.Access.Composition;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Composition;
using ModulithFoundry.Modules.Purchasing.Composition;
using ModulithFoundry.Modules.Sales.Composition;
using ModulithFoundry.Testing.Architecture;
using Npgsql;

namespace ArchitectureTests;

public sealed class ModulePersistenceRulesTests
{
    private const string OwnedSchemaAnnotation = "ModulithFoundry:OwnedSchema";

    private static readonly ModulePersistenceAdapter[] PersistenceAdapters =
    [
        new("Access", services => services.AddAccessPersistence()),
        new(
            "Inventory",
            services => services.AddInventoryPersistence()),
        new(
            "Purchasing",
            services => services.AddPurchasingPersistence()),
        new("Sales", services => services.AddSalesPersistence()),
    ];

    [Fact]
    public void ModulePersistenceAdapters_WhenDiscoveredModuleHasNoAdapter_ReportViolation()
    {
        string[] discoveredModules = [.. PersistenceAdapters.Select(module => module.Name), "Shipping"];
        string[] adaptedModules = [.. PersistenceAdapters.Select(module => module.Name)];

        Assert.Contains(
            "Shipping has no persistence architecture-test adapter",
            ModuleCoveragePolicy.AdapterViolations(discoveredModules, adaptedModules));
    }

    [Fact]
    public void ModulePersistenceAdapters_WhenAdapterHasNoDiscoveredModule_ReportViolation()
    {
        string[] discoveredModules = [.. PersistenceAdapters.Select(module => module.Name)];
        string[] adaptedModules = [.. discoveredModules, "Shipping"];

        Assert.Contains(
            "Shipping persistence architecture-test adapter has no discovered module",
            ModuleCoveragePolicy.AdapterViolations(discoveredModules, adaptedModules));
    }

    [Fact]
    public void ModulePersistenceAdapters_WhenInspected_CoverEveryDiscoveredModule()
    {
        string[] discoveredModules = [.. RepositoryTopology.Modules().Select(module => module.Name)];
        string[] adaptedModules = [.. PersistenceAdapters.Select(module => module.Name)];

        Assert.Empty(ModuleCoveragePolicy.AdapterViolations(discoveredModules, adaptedModules));
    }

    [Fact]
    public async Task ModulePersistence_WhenEfMetadataIsInspected_RespectsModuleAndOrganizationBoundaries()
    {
        await using NpgsqlDataSource dataSource = NpgsqlDataSource.Create(
            "Host=localhost;Database=architecture_tests;Username=unused;Password=unused");

        foreach (ModuleDefinition module in RepositoryTopology.Modules())
        {
            ModulePersistenceAdapter adapter = PersistenceAdapters.Single(candidate =>
                candidate.Name == module.Name);
            var services = new ServiceCollection();
            services.AddSingleton(dataSource);
            adapter.Register(services);
            Type dbContextType = services
                .Select(descriptor => descriptor.ServiceType)
                .Single(type => typeof(DbContext).IsAssignableFrom(type));
            await using ServiceProvider provider = services.BuildServiceProvider();
            await using AsyncServiceScope scope = provider.CreateAsyncScope();
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(dbContextType);

            RelationalOptionsExtension relationalOptions = context.GetService<IDbContextOptions>()
                .Extensions
                .OfType<RelationalOptionsExtension>()
                .Single();
            Assert.Equal(module.Schema, relationalOptions.MigrationsHistoryTableSchema);
            Assert.Equal(module.Schema, context.Model.GetDefaultSchema());

            Assert.All(
                context.Model.GetEntityTypes(),
                entity => Assert.Equal(module.Schema, entity.GetSchema()));
            Assert.All(context.Model.GetEntityTypes(), entity =>
            {
                bool isOrganizationOwned = typeof(IOrganizationOwned)
                    .IsAssignableFrom(entity.ClrType);
                IQueryFilter? organizationFilter = entity.FindDeclaredQueryFilter(
                    "OrganizationScope");

                Assert.Equal(isOrganizationOwned, organizationFilter is not null);
            });

            IMigrationsAssembly migrationsAssembly = context.GetService<IMigrationsAssembly>();
            string activeProvider = context.Database.ProviderName
                ?? throw new InvalidOperationException($"{module.Name} has no EF provider.");
            foreach (TypeInfo migrationType in migrationsAssembly.Migrations.Values)
            {
                Migration migration = migrationsAssembly.CreateMigration(
                    migrationType,
                    activeProvider);
                Assert.Empty(SchemaOwnershipViolations(module.Schema, migration.UpOperations));
                Assert.Empty(SchemaOwnershipViolations(module.Schema, migration.DownOperations));
            }
        }
    }

    [Fact]
    public void SchemaOwnership_WhenMigrationNamesAnotherSchema_ReportsViolation()
    {
        MigrationOperation[] operations =
        [
            new CreateTableOperation
            {
                Name = "reservation",
                Schema = "inventory",
            }
        ];

        Assert.Contains(
            "CreateTableOperation names schema 'inventory' instead of owned schema 'sales'",
            SchemaOwnershipViolations("sales", operations));
    }

    [Fact]
    public void SchemaOwnership_WhenNestedForeignKeyNamesAnotherSchema_ReportsViolation()
    {
        var createTable = new CreateTableOperation
        {
            Name = "reservation",
            Schema = "sales",
        };
        createTable.ForeignKeys.Add(new AddForeignKeyOperation
        {
            Name = "FK_reservation_stock",
            Table = "reservation",
            Schema = "sales",
            Columns = ["stock_id"],
            PrincipalTable = "stock",
            PrincipalSchema = "inventory",
            PrincipalColumns = ["id"],
        });

        Assert.Contains(
            "AddForeignKeyOperation names schema 'inventory' instead of owned schema 'sales'",
            SchemaOwnershipViolations("sales", [createTable]));
    }

    [Fact]
    public void SchemaOwnership_WhenMigrationUsesRawSql_ReportsViolation()
    {
        MigrationOperation[] operations =
        [
            new SqlOperation
            {
                Sql = "SELECT * FROM inventory.stock;",
            }
        ];

        Assert.Contains(
            "SqlOperation contains raw SQL; schema ownership cannot be verified",
            SchemaOwnershipViolations("sales", operations));
    }

    [Fact]
    public void SchemaOwnership_WhenRawSqlDeclaresOwnedSchema_HasNoViolation()
    {
        var operation = new SqlOperation
        {
            Sql = "UPDATE sales.orders SET status = 'pending';",
        };
        operation.AddAnnotation(OwnedSchemaAnnotation, "sales");

        Assert.Empty(SchemaOwnershipViolations("sales", [operation]));
    }

    [Fact]
    public void SchemaOwnership_WhenRawSqlDeclaresAnotherSchema_ReportsViolation()
    {
        var operation = new SqlOperation
        {
            Sql = "UPDATE inventory.stock SET available = false;",
        };
        operation.AddAnnotation(OwnedSchemaAnnotation, "inventory");

        Assert.Contains(
            "SqlOperation declares schema 'inventory' instead of owned schema 'sales'",
            SchemaOwnershipViolations("sales", [operation]));
    }

    private static List<string> SchemaOwnershipViolations(
        string ownedSchema,
        IEnumerable<MigrationOperation> operations)
    {
        var violations = new List<string>();

        foreach (MigrationOperation operation in DescendantsAndSelf(operations))
        {
            if (operation is SqlOperation)
            {
                string? declaredSchema = operation.FindAnnotation(OwnedSchemaAnnotation)?.Value
                    as string;
                if (declaredSchema is null)
                {
                    violations.Add(
                        "SqlOperation contains raw SQL; schema ownership cannot be verified");
                }
                else if (!string.Equals(declaredSchema, ownedSchema, StringComparison.Ordinal))
                {
                    violations.Add(
                        $"SqlOperation declares schema '{declaredSchema}' instead of owned schema '{ownedSchema}'");
                }

                continue;
            }

            IEnumerable<string> schemaReferences = operation is EnsureSchemaOperation ensureSchema
                ? [ensureSchema.Name]
                : operation.GetType()
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(property =>
                        property.PropertyType == typeof(string)
                        && property.Name.Contains("Schema", StringComparison.Ordinal))
                    .Select(property => property.GetValue(operation) as string)
                    .OfType<string>();

            foreach (string schema in schemaReferences.Where(schema =>
                !string.Equals(schema, ownedSchema, StringComparison.Ordinal)))
            {
                violations.Add(
                    $"{operation.GetType().Name} names schema '{schema}' instead of owned schema '{ownedSchema}'");
            }
        }

        return violations;
    }

    private static IEnumerable<MigrationOperation> DescendantsAndSelf(
        IEnumerable<MigrationOperation> operations)
    {
        var visited = new HashSet<MigrationOperation>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<MigrationOperation>(operations.Reverse());

        while (pending.TryPop(out MigrationOperation? operation))
        {
            if (!visited.Add(operation))
            {
                continue;
            }

            yield return operation;

            foreach (PropertyInfo property in operation.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => property.GetIndexParameters().Length == 0))
            {
                object? value = property.GetValue(operation);
                if (value is MigrationOperation nested)
                {
                    pending.Push(nested);
                }
                else if (value is IEnumerable items and not string)
                {
                    foreach (MigrationOperation item in items.OfType<MigrationOperation>())
                    {
                        pending.Push(item);
                    }
                }
            }
        }
    }

    private sealed record ModulePersistenceAdapter(
        string Name,
        Action<IServiceCollection> Register);
}
