using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Rootbolt.Messaging.EntityFrameworkCore;
using Rootbolt.Messaging.EntityFrameworkCore.Postgres;

namespace ModulithFoundry.Samples.OutboxDemo;

public sealed class ExportDbContext(DbContextOptions<ExportDbContext> options) : DbContext(options)
{
    public static ExportDbContext Create(string connection) =>
        new(
            new DbContextOptionsBuilder<ExportDbContext>()
                .UseNpgsql(
                    connection,
                    postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", "exports")
                )
                .Options
        );

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ConfigurePostgresOutbox("exports", "outgoing_work");
        var request = modelBuilder.Entity<ExportRequest>();
        request.ToTable("requests", "exports");
        request.HasKey(row => row.Id);
        request.Property(row => row.Id).ValueGeneratedNever();
        request.Property(row => row.Version).IsConcurrencyToken();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        this.ValidateOutboxChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default
    )
    {
        this.ValidateOutboxChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}

public sealed class ExportDesignTimeFactory : IDesignTimeDbContextFactory<ExportDbContext>
{
    public ExportDbContext CreateDbContext(string[] args) =>
        ExportDbContext.Create("Host=localhost;Database=not-opened;Username=not-used");
}
