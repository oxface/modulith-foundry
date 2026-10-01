using Microsoft.Extensions.DependencyInjection;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Sales.Composition;
using ModulithFoundry.Modules.Sales.Contracts;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ModulithFoundry.PersistenceTests;

public sealed class CustomerPersistenceTests
{
    [Fact]
    public async Task CreateCustomer_ValidInput_IsRetrievableByCanonicalCodeInFreshScope()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(postgres.GetConnectionString(), organization);
        CustomerView created = Assert.IsType<CreateCustomerResult.Created>(
            await CreateAsync(services, organization, "  north-trading  ", "  North Trading  ")).Customer;
        Assert.Equal("NORTH-TRADING", created.Code);
        Assert.Equal("North Trading", created.Name);
        Assert.NotEqual(Guid.Empty, created.CustomerId.Value);
        Assert.Equal(created, Assert.IsType<GetCustomerResult.Found>(
            await GetAsync(services, organization, "north-trading")).Customer);
    }

    [Fact]
    public async Task CreateCustomer_SameCodeConcurrently_OneCreationAndOneCodeUnavailable()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(postgres.GetConnectionString(), organization);
        CreateCustomerResult[] results = await Task.WhenAll(
            CreateAsync(services, organization, "north-trading", "First"),
            CreateAsync(services, organization, " NORTH-TRADING ", "Second"));
        CustomerView winner = Assert.Single(results.OfType<CreateCustomerResult.Created>()).Customer;
        Assert.Equal("NORTH-TRADING", Assert.Single(results.OfType<CreateCustomerResult.CodeUnavailable>()).Code);
        Assert.Equal(winner, Assert.IsType<GetCustomerResult.Found>(
            await GetAsync(services, organization, "north-trading")).Customer);
    }

    [Fact]
    public async Task CustomerAdministration_ForeignOrUnresolvedContextAndPermissionDenial_FailsClosed()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(postgres.GetConnectionString(), organization);
        CustomerView original = Assert.IsType<CreateCustomerResult.Created>(
            await CreateAsync(services, organization, "buyer", "First Buyer")).Customer;

        OrganizationAccessContext foreign = CreateOrganizationContext();
        Assert.IsType<CreateCustomerResult.PermissionDenied>(await CreateAsync(services, foreign, "forged", "Forged"));
        Assert.IsType<GetCustomerResult.PermissionDenied>(await GetAsync(services, foreign, "buyer"));
        services.GetRequiredService<TestOrganizationContextAccessor>().OrganizationContext = foreign;
        Assert.IsType<GetCustomerResult.NotFound>(await GetAsync(services, foreign, "buyer"));
        CustomerView other = Assert.IsType<CreateCustomerResult.Created>(
            await CreateAsync(services, foreign, "buyer", "Other Buyer")).Customer;
        Assert.NotEqual(original.CustomerId, other.CustomerId);
        Assert.Equal(other, Assert.IsType<GetCustomerResult.Found>(await GetAsync(services, foreign, "buyer")).Customer);

        services.GetRequiredService<TestOrganizationContextAccessor>().OrganizationContext = null;
        Assert.IsType<GetCustomerResult.PermissionDenied>(await GetAsync(services, organization, "buyer"));
        Assert.IsType<CreateCustomerResult.PermissionDenied>(await CreateAsync(services, organization, "unresolved", "Unresolved"));
        services.GetRequiredService<TestOrganizationContextAccessor>().OrganizationContext = organization;
        Assert.IsType<GetCustomerResult.PermissionDenied>(await GetAsync(services, organization with { UserId = new(Guid.CreateVersion7()) }, "buyer"));
        services.GetRequiredService<TestOrganizationAuthorization>().Allow = false;
        Assert.IsType<CreateCustomerResult.PermissionDenied>(await CreateAsync(services, organization, "denied", "Denied"));
        Assert.IsType<GetCustomerResult.PermissionDenied>(await GetAsync(services, organization, "buyer"));
        services.GetRequiredService<TestOrganizationAuthorization>().Allow = true;
        Assert.IsType<GetCustomerResult.NotFound>(await GetAsync(services, organization, "denied"));
        Assert.IsType<GetCustomerResult.NotFound>(await GetAsync(services, organization, "forged"));
        Assert.IsType<GetCustomerResult.NotFound>(await GetAsync(services, organization, "unresolved"));
        Assert.Equal(original, Assert.IsType<GetCustomerResult.Found>(await GetAsync(services, organization, "buyer")).Customer);
    }

    [Fact]
    public async Task CreateCustomer_InvalidCodeOrName_ReturnsFieldSpecificFailureWithoutCreatingCustomer()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(postgres.GetConnectionString(), organization);
        (string Code, string Name, string Field)[] invalidInputs =
        [
            ("", "Buyer", "code"),
            ("-buyer", "Buyer", "code"),
            ("buyer/other", "Buyer", "code"),
            ("büyér", "Buyer", "code"),
            (new string('b', 65), "Buyer", "code"),
            ("buyer", " ", "name"),
            ("buyer", new string('n', 201), "name"),
            ("buyer", "North\nTrading", "name"),
            (null!, "Buyer", "code"),
            ("buyer", null!, "name"),
        ];
        foreach ((string code, string name, string field) in invalidInputs)
        {
            Assert.Equal(field, Assert.IsType<CreateCustomerResult.Invalid>(
                await CreateAsync(services, organization, code, name)).Field);
        }
        Assert.IsType<GetCustomerResult.NotFound>(await GetAsync(services, organization, "buyer"));
        Assert.Equal("code", Assert.IsType<GetCustomerResult.Invalid>(await GetAsync(services, organization, "buyer/other")).Field);
    }

    [Fact]
    public async Task CreateCustomer_AuditWriteFails_RollsBackCustomerAndAllowsFreshScopeRetry()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(postgres.GetConnectionString(), organization);
        await ExecuteFaultSetupAsync(postgres.GetConnectionString(), """
            CREATE FUNCTION sales.reject_customer_audit() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'injected audit persistence failure'; END; $$;
            CREATE TRIGGER reject_customer_audit BEFORE INSERT ON sales.audit_entries
            FOR EACH ROW EXECUTE FUNCTION sales.reject_customer_audit();
            """);
        await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() => CreateAsync(services, organization, "buyer", "Buyer"));
        Assert.IsType<GetCustomerResult.NotFound>(await GetAsync(services, organization, "buyer"));
        await ExecuteFaultSetupAsync(postgres.GetConnectionString(), "DROP TRIGGER reject_customer_audit ON sales.audit_entries");
        CustomerView created = Assert.IsType<CreateCustomerResult.Created>(await CreateAsync(services, organization, "buyer", "Buyer")).Customer;
        Assert.Equal(created, Assert.IsType<GetCustomerResult.Found>(await GetAsync(services, organization, "buyer")).Customer);
    }

    [Fact]
    public async Task CreateCustomer_AuditRequiredAtCommit_PersistsMatchingAuditAtomically()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(postgres.GetConnectionString(), organization);
        // Enforce the expected product effect as a database fault seam, not a row assertion.
        await ExecuteFaultSetupAsync(postgres.GetConnectionString(), """
            CREATE FUNCTION sales.require_customer_audit() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM sales.audit_entries
                    WHERE subject_id = NEW.id AND organization_id = NEW.organization_id
                    AND action = 'customer.created' AND outcome = 'succeeded'
                    AND source_module = 'sales' AND schema_version = 1
                    AND details ->> 'Code' = NEW.code
                ) THEN RAISE EXCEPTION 'customer committed without expected audit'; END IF;
                RETURN NULL;
            END; $$;
            CREATE CONSTRAINT TRIGGER require_customer_audit AFTER INSERT ON sales.customers
            DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION sales.require_customer_audit();
            """);
        Assert.IsType<CreateCustomerResult.Created>(await CreateAsync(services, organization, "buyer", "Buyer"));
    }

    private static async Task ExecuteFaultSetupAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateCustomer_DuplicateThenAnotherCreateInSameScope_DoesNotResaveRejectedChanges()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        OrganizationAccessContext organization = CreateOrganizationContext();
        await using ServiceProvider services = await CreateServicesAsync(postgres.GetConnectionString(), organization);
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        ICustomerAdministration customers = scope.ServiceProvider.GetRequiredService<ICustomerAdministration>();
        Assert.IsType<CreateCustomerResult.Created>(await customers.CreateAsync(
            new(organization.UserId, organization.OrganizationId, "buyer", "Buyer"), TestContext.Current.CancellationToken));
        Assert.IsType<CreateCustomerResult.CodeUnavailable>(await customers.CreateAsync(
            new(organization.UserId, organization.OrganizationId, "buyer", "Duplicate"), TestContext.Current.CancellationToken));
        CustomerView created = Assert.IsType<CreateCustomerResult.Created>(await customers.CreateAsync(
            new(organization.UserId, organization.OrganizationId, "buyer-2", "Second Buyer"), TestContext.Current.CancellationToken)).Customer;
        Assert.Equal(created, Assert.IsType<GetCustomerResult.Found>(
            await GetAsync(services, organization, "buyer-2")).Customer);
    }

    private static OrganizationAccessContext CreateOrganizationContext() =>
        new(new UserId(Guid.CreateVersion7()), new OrganizationId(Guid.CreateVersion7()),
            new MembershipId(Guid.CreateVersion7()), "Sales Tests", "sales-tests", [SalesRoleIds.Clerk]);

    private static async Task<ServiceProvider> CreateServicesAsync(string connectionString, OrganizationAccessContext organization)
    {
        var services = new ServiceCollection();
        services.AddSingleton(NpgsqlDataSource.Create(connectionString));
        services.AddSingleton(new TestOrganizationContextAccessor(organization));
        services.AddSingleton<IOrganizationContextAccessor>(provider => provider.GetRequiredService<TestOrganizationContextAccessor>());
        services.AddSingleton<TestOrganizationAuthorization>();
        services.AddSingleton<IOrganizationAuthorization>(provider => provider.GetRequiredService<TestOrganizationAuthorization>());
        services.AddSalesModule();
        ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        await provider.MigrateSalesAsync(TestContext.Current.CancellationToken);
        return provider;
    }

    private static async Task<CreateCustomerResult> CreateAsync(ServiceProvider services, OrganizationAccessContext organization, string code, string name)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ICustomerAdministration>().CreateAsync(
            new(organization.UserId, organization.OrganizationId, code, name), TestContext.Current.CancellationToken);
    }

    private static async Task<GetCustomerResult> GetAsync(ServiceProvider services, OrganizationAccessContext organization, string code)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ICustomerAdministration>().GetByCodeAsync(
            organization.UserId, organization.OrganizationId, code, TestContext.Current.CancellationToken);
    }

    private sealed class TestOrganizationContextAccessor(OrganizationAccessContext organization) : IOrganizationContextAccessor
    {
        public OrganizationAccessContext? OrganizationContext { get; set; } = organization;
    }

    private sealed class TestOrganizationAuthorization : IOrganizationAuthorization
    {
        public bool Allow { get; set; } = true;
        public Task<bool> HasPermissionAsync(UserId userId, OrganizationId organizationId, string permissionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Allow && permissionId == SalesPermissionIds.CustomersManage);
    }
}
