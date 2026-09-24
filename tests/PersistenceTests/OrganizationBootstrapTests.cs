using Microsoft.Extensions.DependencyInjection;
using ModulithFoundry.Modules.Access.Composition;
using ModulithFoundry.Modules.Access.Contracts;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ModulithFoundry.PersistenceTests;

public sealed class OrganizationBootstrapTests
{
    [Fact]
    public async Task CreateOrganization_NewSlug_CreatesFirstAdministratorMembership()
    {
        await using PostgreSqlContainer postgres = CreatePostgresContainer();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using ServiceProvider services = await CreateAccessServicesAsync(
            postgres.GetConnectionString());
        UserIdentityLink user = await LinkUserAsync(
            services,
            "creator-subject",
            "creator@example.test");

        OrganizationMembership created = await CreateOrganizationAsync(
            services,
            user.UserId,
            "  Acme Industrial  ",
            "  Acme_Industrial  ");
        IReadOnlyList<OrganizationMembership> accessible = await ListOrganizationsAsync(
            services,
            user.UserId);

        Assert.NotEqual(Guid.Empty, created.OrganizationId.Value);
        Assert.Equal("Acme Industrial", created.Name);
        Assert.Equal("acme-industrial", created.Slug);
        Assert.Equal([SystemRoleIds.OrganizationAdministrator], created.RoleIds);
        OrganizationMembership listed = Assert.Single(accessible);
        Assert.Equal(created.OrganizationId, listed.OrganizationId);
        Assert.Equal(created.Name, listed.Name);
        Assert.Equal(created.Slug, listed.Slug);
        Assert.Equal(created.RoleIds, listed.RoleIds);
        OrganizationCreatedAudit audit = await ReadOrganizationCreatedAuditAsync(
            postgres.GetConnectionString(),
            created.OrganizationId,
            user.UserId);
        Assert.Equal(1, audit.SchemaVersion);
        Assert.Equal("access", audit.SourceModule);
        Assert.Equal("succeeded", audit.Outcome);
        Assert.Null(audit.ReasonCode);
        Assert.Equal(created.Name, audit.OrganizationName);
        Assert.Equal(created.Slug, audit.OrganizationSlug);
    }

    [Fact]
    public async Task CreateOrganization_InvalidNameOrSlug_ReturnsSpecificFailureWithoutChanges()
    {
        await using PostgreSqlContainer postgres = CreatePostgresContainer();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using ServiceProvider services = await CreateAccessServicesAsync(
            postgres.GetConnectionString());
        UserIdentityLink user = await LinkUserAsync(
            services,
            "invalid-input-subject",
            "invalid-input@example.test");

        CreateOrganizationResult invalidName = await ExecuteCreateOrganizationAsync(
            services,
            user.UserId,
            "  ",
            "valid-slug");
        CreateOrganizationResult shortSlug = await ExecuteCreateOrganizationAsync(
            services,
            user.UserId,
            "Valid Organization",
            "ab");
        CreateOrganizationResult unsupportedSlug = await ExecuteCreateOrganizationAsync(
            services,
            user.UserId,
            "Valid Organization",
            "invalid/slug");

        Assert.IsType<CreateOrganizationResult.InvalidName>(invalidName);
        Assert.IsType<CreateOrganizationResult.InvalidSlug>(shortSlug);
        Assert.IsType<CreateOrganizationResult.InvalidSlug>(unsupportedSlug);
        Assert.Empty(await ListOrganizationsAsync(services, user.UserId));
        Assert.Equal(
            0,
            await CountOrganizationCreatedAuditsAsync(postgres.GetConnectionString()));
    }

    [Fact]
    public async Task CreateOrganization_ConcurrentCanonicalSlug_RejectsLoserWithoutMembership()
    {
        await using PostgreSqlContainer postgres = CreatePostgresContainer();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using ServiceProvider services = await CreateAccessServicesAsync(
            postgres.GetConnectionString());
        UserIdentityLink firstUser = await LinkUserAsync(
            services,
            "first-subject",
            "first@example.test");
        UserIdentityLink secondUser = await LinkUserAsync(
            services,
            "second-subject",
            "second@example.test");

        OrganizationCreationAttempt[] attempts = await Task.WhenAll(
            CaptureCreationAsync(
                services,
                firstUser.UserId,
                "First Organization",
                "Contested_Slug"),
            CaptureCreationAsync(
                services,
                secondUser.UserId,
                "Second Organization",
                "contested-slug"));

        OrganizationCreationAttempt winner = Assert.Single(
            attempts,
            attempt => attempt.Result is CreateOrganizationResult.Created);
        OrganizationCreationAttempt loser = Assert.Single(
            attempts,
            attempt => attempt.Result is CreateOrganizationResult.SlugUnavailable);
        CreateOrganizationResult.Created created = Assert.IsType<CreateOrganizationResult.Created>(
            winner.Result);
        CreateOrganizationResult.SlugUnavailable unavailable =
            Assert.IsType<CreateOrganizationResult.SlugUnavailable>(loser.Result);
        Assert.Equal("contested-slug", unavailable.Slug);
        Assert.Equal("contested-slug", created.Organization.Slug);
        Assert.Single(await ListOrganizationsAsync(services, winner.UserId));
        Assert.Empty(await ListOrganizationsAsync(services, loser.UserId));
        Assert.Equal(
            1,
            await CountOrganizationCreatedAuditsAsync(postgres.GetConnectionString()));
    }

    private static PostgreSqlContainer CreatePostgresContainer() =>
        new PostgreSqlBuilder("postgres:18.6").Build();

    private static async Task<ServiceProvider> CreateAccessServicesAsync(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(NpgsqlDataSource.Create(connectionString));
        services.AddAccessModule();

        ServiceProvider provider = services.BuildServiceProvider();
        await provider.MigrateAccessAsync(TestContext.Current.CancellationToken);
        return provider;
    }

    private static async Task<UserIdentityLink> LinkUserAsync(
        IServiceProvider services,
        string subject,
        string email)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IExternalIdentityLinker>()
            .LinkAsync(
                ExternalIdentity.Create("https://issuer.example", subject, email, subject),
                TestContext.Current.CancellationToken);
    }

    private static async Task<OrganizationMembership> CreateOrganizationAsync(
        IServiceProvider services,
        UserId userId,
        string name,
        string proposedSlug)
    {
        CreateOrganizationResult result = await ExecuteCreateOrganizationAsync(
            services,
            userId,
            name,
            proposedSlug);
        return Assert.IsType<CreateOrganizationResult.Created>(result).Organization;
    }

    private static async Task<CreateOrganizationResult> ExecuteCreateOrganizationAsync(
        IServiceProvider services,
        UserId userId,
        string name,
        string proposedSlug)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IOrganizationCreation>()
            .CreateOrganizationAsync(
                new CreateOrganizationCommand(userId, name, proposedSlug),
                TestContext.Current.CancellationToken);
    }

    private static async Task<IReadOnlyList<OrganizationMembership>> ListOrganizationsAsync(
        IServiceProvider services,
        UserId userId)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IOrganizationQueries>()
            .ListAccessibleToAsync(userId, TestContext.Current.CancellationToken);
    }

    private static async Task<OrganizationCreationAttempt> CaptureCreationAsync(
        IServiceProvider services,
        UserId userId,
        string name,
        string proposedSlug)
    {
        CreateOrganizationResult result = await ExecuteCreateOrganizationAsync(
            services,
            userId,
            name,
            proposedSlug);
        return new OrganizationCreationAttempt(userId, result);
    }

    private static async Task<long> CountOrganizationCreatedAuditsAsync(string connectionString)
    {
        const string sql = """
            SELECT count(*)
            FROM access.audit_entries
            WHERE action = 'organization.created'
            """;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = sql;

        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("Audit count query returned no value."));
    }

    private static async Task<OrganizationCreatedAudit> ReadOrganizationCreatedAuditAsync(
        string connectionString,
        OrganizationId organizationId,
        UserId actorUserId)
    {
        const string sql = """
            SELECT schema_version,
                   source_module,
                   outcome,
                   reason_code,
                   details->>'name',
                   details->>'slug'
            FROM access.audit_entries
            WHERE organization_id = @organization_id
              AND actor_user_id = @actor_user_id
              AND action = 'organization.created'
            """;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("organization_id", organizationId.Value);
        command.Parameters.AddWithValue("actor_user_id", actorUserId.Value);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(
            TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        var audit = new OrganizationCreatedAudit(
            reader.GetInt16(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5));
        Assert.False(await reader.ReadAsync(TestContext.Current.CancellationToken));
        return audit;
    }

    private sealed record OrganizationCreationAttempt(
        UserId UserId,
        CreateOrganizationResult Result);

    private sealed record OrganizationCreatedAudit(
        short SchemaVersion,
        string SourceModule,
        string Outcome,
        string? ReasonCode,
        string OrganizationName,
        string OrganizationSlug);
}
