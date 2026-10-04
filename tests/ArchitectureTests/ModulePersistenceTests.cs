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
        Type[] ownedTypes =
            module == "inventory"
                ? [typeof(StockReference)]
                : [typeof(CustomerAddressReference), typeof(CustomerReference)];
        Type[] expectedTypes =
            module == "inventory"
                ? [typeof(ReferenceCategory), typeof(StockReference)]
                : [typeof(CustomerAddressReference), typeof(CustomerReference)];
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
        foreach (Type ownedType in ownedTypes)
        {
            var owned = context.Model.FindEntityType(ownedType)!;
            Assert.True(owned.FindProperty("OrganizationKey")!.IsConcurrencyToken);
            Assert.Contains(
                owned.GetDeclaredQueryFilters(),
                filter => filter.Key == "OrganizationScope"
            );
        }
        if (module == "inventory")
            Assert.Empty(
                context.Model.FindEntityType(typeof(ReferenceCategory))!.GetDeclaredQueryFilters()
            );
        else
        {
            var address = context.Model.FindEntityType(typeof(CustomerAddressReference))!;
            var relation = Assert.Single(address.GetForeignKeys());
            Assert.Equal(typeof(CustomerReference), relation.PrincipalEntityType.ClrType);
            Assert.Equal(
                ["OrganizationKey", "CustomerId"],
                relation.Properties.Select(property => property.Name)
            );
            Assert.Equal(
                ["OrganizationKey", "Id"],
                relation.PrincipalKey.Properties.Select(property => property.Name)
            );
            Assert.True(relation.IsRequired);
            Assert.Equal(DeleteBehavior.Restrict, relation.DeleteBehavior);
        }

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
                        Assert.All(
                            table.ForeignKeys,
                            foreign => CustomerAddressConstraint(foreign, module)
                        );
                        break;
                    case CreateIndexOperation index:
                        Assert.Equal(module, index.Schema);
                        break;
                    case DropTableOperation table:
                        Assert.Equal(module, table.Schema);
                        break;
                    case AddUniqueConstraintOperation key:
                        Assert.Equal(module, key.Schema);
                        Assert.Equal("customer_reference", key.Table);
                        Assert.Equal(["OrganizationKey", "Id"], key.Columns);
                        break;
                    case DropUniqueConstraintOperation key:
                        Assert.Equal(module, key.Schema);
                        Assert.Equal("customer_reference", key.Table);
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

    private static void CustomerAddressConstraint(AddForeignKeyOperation foreign, string module)
    {
        Assert.Equal("sales", module);
        Assert.Equal(module, foreign.Schema);
        Assert.Equal(module, foreign.PrincipalSchema);
        Assert.Equal("customer_address_reference", foreign.Table);
        Assert.Equal("customer_reference", foreign.PrincipalTable);
        Assert.Equal(["OrganizationKey", "CustomerId"], foreign.Columns);
        Assert.NotNull(foreign.PrincipalColumns);
        Assert.Equal(["OrganizationKey", "Id"], foreign.PrincipalColumns);
        Assert.Equal(ReferentialAction.Restrict, foreign.OnDelete);
    }
}
