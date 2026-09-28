using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModulithFoundry.Modules.Access.Composition;
using ModulithFoundry.Modules.Access.Contracts;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ModulithFoundry.PersistenceTests;

public sealed class ExternalIdentityLinkingTests
{
    [Theory]
    [InlineData("", "subject-1")]
    [InlineData("https://issuer.example", "")]
    public void ExternalIdentity_MissingStableIdentifier_IsRejected(string issuer, string subject)
    {
        Assert.Throws<ArgumentException>(() =>
            ExternalIdentity.Create(issuer, subject, "first@example.com", "First User"));
    }

    [Fact]
    public async Task LinkExternalIdentity_FirstAuthentication_CreatesProductUser()
    {
        await using PostgreSqlContainer postgres = CreatePostgresContainer();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using ServiceProvider services = await CreateAccessServicesAsync(
            postgres.GetConnectionString());

        UserIdentityLink linked = await LinkAsync(
            services,
            ExternalIdentity.Create(
                "https://issuer.example",
                "subject-1",
                "first@example.com",
                "First User"));

        Assert.NotEqual(Guid.Empty, linked.UserId.Value);
        Assert.Equal("first@example.com", linked.Email);
        Assert.Equal("First User", linked.DisplayName);
    }

    [Fact]
    public async Task LinkExternalIdentity_RepeatedAuthenticationWithChangedProfile_ReusesProductUser()
    {
        await using PostgreSqlContainer postgres = CreatePostgresContainer();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using ServiceProvider services = await CreateAccessServicesAsync(
            postgres.GetConnectionString());

        UserIdentityLink first = await LinkAsync(
            services,
            ExternalIdentity.Create(
                "https://issuer.example",
                "subject-1",
                "first@example.com",
                "First User"));
        UserIdentityLink repeated = await LinkAsync(
            services,
            ExternalIdentity.Create(
                "https://issuer.example",
                "subject-1",
                "changed@example.com",
                "Changed User"));

        Assert.Equal(first.UserId, repeated.UserId);
        Assert.Equal("changed@example.com", repeated.Email);
        Assert.Equal("Changed User", repeated.DisplayName);
    }

    [Fact]
    public async Task LinkExternalIdentity_ConcurrentFirstAuthentication_CreatesOneProductUser()
    {
        await using PostgreSqlContainer postgres = CreatePostgresContainer();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using ServiceProvider services = await CreateAccessServicesAsync(
            postgres.GetConnectionString());
        ExternalIdentity identity = ExternalIdentity.Create(
            "https://issuer.example",
            "subject-1",
            "first@example.com",
            "First User");

        UserIdentityLink[] links = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => LinkAsync(services, identity)));

        Assert.Single(links.Select(link => link.UserId).Distinct());

        await using var connection = new NpgsqlConnection(postgres.GetConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM access.users; SELECT COUNT(*) FROM access.external_identities;";
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(
            TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, reader.GetInt64(0));
        Assert.True(await reader.NextResultAsync(TestContext.Current.CancellationToken));
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, reader.GetInt64(0));
    }

    private static PostgreSqlContainer CreatePostgresContainer() =>
        new PostgreSqlBuilder("postgres:18.6").Build();

    private static async Task<ServiceProvider> CreateAccessServicesAsync(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(NpgsqlDataSource.Create(connectionString));
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Invitations:PublicApplicationUrl"] = "https://example.test",
                ["Email:Smtp:Host"] = "localhost",
                ["Email:Smtp:Port"] = "1025",
                ["Email:Smtp:Security"] = "None",
                ["Email:Smtp:FromAddress"] = "no-reply@example.test",
            })
            .Build();
        services.AddAccessModule(configuration);

        ServiceProvider provider = services.BuildServiceProvider();
        await provider.MigrateAccessAsync(TestContext.Current.CancellationToken);
        return provider;
    }

    private static async Task<UserIdentityLink> LinkAsync(
        IServiceProvider services,
        ExternalIdentity identity)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IExternalIdentityLinking>()
            .LinkAsync(identity, TestContext.Current.CancellationToken);
    }
}
