using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using ModulithFoundry.Samples.Wholesale.PersistenceDemo.Inventory;
using ModulithFoundry.Samples.Wholesale.PersistenceDemo.Sales;

namespace ModulithFoundry.ArchitectureTests;

public sealed class ModulePersistenceTests
{
    [Theory]
    [InlineData("inventory")]
    [InlineData("sales")]
    public void ModuleMappingsAndMigrationArtifactsStayWithinTheirSchema(string module)
    {
        using DbContext context =
            module == "inventory"
                ? new InventoryDesignTimeFactory().CreateDbContext([])
                : new SalesDesignTimeFactory().CreateDbContext([]);
        Type ownedType = module == "inventory" ? typeof(StockReference) : typeof(CustomerReference);
        Type[] expectedTypes =
            module == "inventory"
                ? [typeof(ReferenceCategory), typeof(StockReference)]
                : [typeof(CustomerReference)];
        Assert.Equal(
            expectedTypes,
            context
                .Model.GetEntityTypes()
                .Select(entity => entity.ClrType)
                .OrderBy(type => type.Name)
        );
        Assert.All(
            context.Model.GetEntityTypes(),
            entity => Assert.Equal(module, entity.GetSchema())
        );
        var owned = context.Model.FindEntityType(ownedType)!;
        Assert.True(owned.FindProperty("OrganizationKey")!.IsConcurrencyToken);
        Assert.Contains(
            owned.GetDeclaredQueryFilters(),
            filter => filter.Key == "OrganizationScope"
        );
        if (module == "inventory")
            Assert.Empty(
                context.Model.FindEntityType(typeof(ReferenceCategory))!.GetDeclaredQueryFilters()
            );

        var assembly = context.GetService<IMigrationsAssembly>();
        Assert.NotEmpty(assembly.Migrations);
        foreach (var migrationType in assembly.Migrations.Values)
        {
            Migration migration = assembly.CreateMigration(
                migrationType,
                context.Database.ProviderName!
            );
            Assert.NotEmpty(migration.UpOperations);
            Assert.NotEmpty(migration.DownOperations);
            foreach (
                MigrationOperation operation in migration.UpOperations.Concat(
                    migration.DownOperations
                )
            )
            {
                switch (operation)
                {
                    case EnsureSchemaOperation schema:
                        Assert.Equal(module, schema.Name);
                        break;
                    case CreateTableOperation table:
                        Assert.Equal(module, table.Schema);
                        Assert.Empty(table.ForeignKeys);
                        break;
                    case CreateIndexOperation index:
                        Assert.Equal(module, index.Schema);
                        break;
                    case DropTableOperation table:
                        Assert.Equal(module, table.Schema);
                        break;
                    default:
                        Assert.Fail(
                            $"Review schema ownership before allowing {operation.GetType().Name}."
                        );
                        break;
                }
            }
        }
        Assert.False(context.Database.HasPendingModelChanges());
    }
}
