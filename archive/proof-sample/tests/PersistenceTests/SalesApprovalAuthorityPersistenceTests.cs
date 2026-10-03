using System.Threading.Channels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ModulithFoundry.Modules.Access.Composition;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Access.ExtensionPoints;
using ModulithFoundry.Modules.Sales.Composition;
using ModulithFoundry.Modules.Sales.Contracts;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ModulithFoundry.PersistenceTests;

public sealed class SalesApprovalAuthorityPersistenceTests
{
    [Theory]
    [InlineData("-1", "USD", 0, "MaximumAmount")]
    [InlineData("0.001", "USD", 0, "MaximumAmount")]
    [InlineData("100000000000000000", "USD", 0, "MaximumAmount")]
    [InlineData("1", "GBP", 0, "Currency")]
    [InlineData("1", "USD", -1, "ExpectedVersion")]
    public async Task Set_InvalidLimitOrVersion_DoesNotCreateAuthority(
        string maximum,
        string currency,
        long version,
        string field
    )
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString()
        );
        OrganizationAccessContext manager = await CreateManagerAsync(services, "manager");
        var invalid = Assert.IsType<SetSalesApprovalAuthorityResult.Invalid>(
            await SetAsync(
                services,
                manager,
                manager.MembershipId,
                decimal.Parse(maximum, System.Globalization.CultureInfo.InvariantCulture),
                currency,
                version
            )
        );
        Assert.Equal(field, invalid.Field);
        Assert.IsType<GetSalesApprovalAuthorityResult.NotFound>(
            await GetAsync(services, manager, manager.MembershipId)
        );
    }

    [Theory]
    [InlineData("0")]
    [InlineData("99999999999999999.99")]
    public async Task Set_SupportedAmountBoundary_PersistsWithoutRounding(string maximum)
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString()
        );
        OrganizationAccessContext manager = await CreateManagerAsync(services, "manager");
        decimal amount = decimal.Parse(maximum, System.Globalization.CultureInfo.InvariantCulture);
        Assert.IsType<SetSalesApprovalAuthorityResult.Saved>(
            await SetAsync(services, manager, manager.MembershipId, amount, "EUR", 0)
        );
        Assert.Equal(
            amount,
            Assert
                .IsType<GetSalesApprovalAuthorityResult.Found>(
                    await GetAsync(services, manager, manager.MembershipId)
                )
                .Authority.MaximumAmount
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Set_CompetingCommands_OnlyOneVersionWins(bool replace)
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString()
        );
        OrganizationAccessContext manager = await CreateManagerAsync(services, "manager");
        if (replace)
            Assert.IsType<SetSalesApprovalAuthorityResult.Saved>(
                await SetAsync(services, manager, manager.MembershipId, 100m, "USD", 0)
            );
        long version = replace ? 1 : 0;
        SetSalesApprovalAuthorityResult[] results = await Task.WhenAll(
            SetAsync(services, manager, manager.MembershipId, 200m, "USD", version),
            SetAsync(services, manager, manager.MembershipId, 300m, "EUR", version)
        );
        SalesApprovalAuthorityView winner = Assert
            .Single(results.OfType<SetSalesApprovalAuthorityResult.Saved>())
            .Authority;
        Assert.Single(results.OfType<SetSalesApprovalAuthorityResult.VersionConflict>());
        Assert.Equal(version + 1, winner.Version);
        Assert.Equal(
            winner,
            Assert
                .IsType<GetSalesApprovalAuthorityResult.Found>(
                    await GetAsync(services, manager, manager.MembershipId)
                )
                .Authority
        );
    }

    [Fact]
    public async Task Administration_ForeignOrMismatchedContext_DoesNotExposeOrMutateAuthority()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString()
        );
        OrganizationAccessContext first = await CreateManagerAsync(services, "first");
        SalesApprovalAuthorityView original = Assert
            .IsType<SetSalesApprovalAuthorityResult.Saved>(
                await SetAsync(services, first, first.MembershipId, 10m, "USD", 0)
            )
            .Authority;
        OrganizationAccessContext second = await CreateManagerAsync(services, "second");
        Assert.IsType<GetSalesApprovalAuthorityResult.NotFound>(
            await GetAsync(services, second, first.MembershipId)
        );
        Assert.IsType<SetSalesApprovalAuthorityResult.MembershipUnavailable>(
            await SetAsync(services, second, first.MembershipId, 20m, "USD", 0)
        );
        Assert.IsType<RevokeSalesApprovalAuthorityResult.NotFound>(
            await RevokeAsync(services, second, first.MembershipId, 1)
        );
        await using (AsyncServiceScope scope = services.CreateAsyncScope())
        {
            var memberships =
                scope.ServiceProvider.GetRequiredService<IOrganizationMembershipQueries>();
            Assert.False(
                await memberships.IsActiveAsync(
                    first.OrganizationId,
                    first.MembershipId,
                    TestContext.Current.CancellationToken
                )
            );
        }
        Assert.IsType<SetSalesApprovalAuthorityResult.PermissionDenied>(
            await SetAsync(services, first, first.MembershipId, 20m, "USD", 1)
        );
        Assert.IsType<GetSalesApprovalAuthorityResult.PermissionDenied>(
            await GetAsync(services, first, first.MembershipId)
        );
        Assert.IsType<RevokeSalesApprovalAuthorityResult.PermissionDenied>(
            await RevokeAsync(services, first, first.MembershipId, 1)
        );
        services.GetRequiredService<TestOrganizationContextAccessor>().OrganizationContext = null;
        Assert.IsType<SetSalesApprovalAuthorityResult.PermissionDenied>(
            await SetAsync(services, first, first.MembershipId, 20m, "USD", 1)
        );
        services.GetRequiredService<TestOrganizationContextAccessor>().OrganizationContext = first;
        OrganizationAccessContext wrongActor = first with { UserId = second.UserId };
        Assert.IsType<SetSalesApprovalAuthorityResult.PermissionDenied>(
            await SetAsync(services, wrongActor, first.MembershipId, 20m, "USD", 1)
        );
        Assert.Equal(
            original,
            Assert
                .IsType<GetSalesApprovalAuthorityResult.Found>(
                    await GetAsync(services, first, first.MembershipId)
                )
                .Authority
        );
    }

    [Fact]
    public async Task Administration_CurrentRoleRevocation_DeniesDespiteStaleContextRoles()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString()
        );
        OrganizationAccessContext manager = await CreateManagerAsync(services, "manager");
        Assert.IsType<SetSalesApprovalAuthorityResult.Saved>(
            await SetAsync(services, manager, manager.MembershipId, 100m, "USD", 0)
        );
        await using (AsyncServiceScope scope = services.CreateAsyncScope())
        {
            Assert.IsType<ReplaceMembershipRolesResult.Updated>(
                await scope
                    .ServiceProvider.GetRequiredService<IOrganizationMembershipAdministration>()
                    .ReplaceRolesAsync(
                        new(
                            manager.UserId,
                            manager.OrganizationId,
                            manager.MembershipId,
                            [SystemRoleIds.OrganizationAdministrator]
                        ),
                        TestContext.Current.CancellationToken
                    )
            );
        }
        Assert.IsType<SetSalesApprovalAuthorityResult.PermissionDenied>(
            await SetAsync(services, manager, manager.MembershipId, 200m, "USD", 1)
        );
        Assert.IsType<GetSalesApprovalAuthorityResult.PermissionDenied>(
            await GetAsync(services, manager, manager.MembershipId)
        );
        Assert.IsType<RevokeSalesApprovalAuthorityResult.PermissionDenied>(
            await RevokeAsync(services, manager, manager.MembershipId, 1)
        );
    }

    [Fact]
    public async Task Set_MembershipRemovedAndRejoined_DoesNotInheritPriorTenureAuthority()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString()
        );
        OrganizationAccessContext administrator = await CreateManagerAsync(services, "manager");
        IHostedService[] workers = [.. services.GetServices<IHostedService>()];
        foreach (IHostedService worker in workers)
            await worker.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            OrganizationAccessContext member = await InviteAndAcceptAsync(
                services,
                administrator,
                "member",
                [SalesRoleIds.Manager]
            );
            // A Sales Manager needs no Access-admin permission to manage authority for an active tenure.
            services.GetRequiredService<TestOrganizationContextAccessor>().OrganizationContext =
                member;
            Assert.IsType<SetSalesApprovalAuthorityResult.Saved>(
                await SetAsync(services, member, member.MembershipId, 500m, "USD", 0)
            );
            services.GetRequiredService<TestOrganizationContextAccessor>().OrganizationContext =
                administrator;
            await ChangeStatusAsync(
                services,
                administrator,
                member.MembershipId,
                MembershipStatus.Suspended
            );
            Assert.IsType<SetSalesApprovalAuthorityResult.MembershipUnavailable>(
                await SetAsync(services, administrator, member.MembershipId, 600m, "USD", 1)
            );
            services.GetRequiredService<TestOrganizationContextAccessor>().OrganizationContext =
                member;
            Assert.IsType<SetSalesApprovalAuthorityResult.PermissionDenied>(
                await SetAsync(services, member, member.MembershipId, 600m, "USD", 1)
            );
            services.GetRequiredService<TestOrganizationContextAccessor>().OrganizationContext =
                administrator;
            await ChangeStatusAsync(
                services,
                administrator,
                member.MembershipId,
                MembershipStatus.Removed
            );
            Assert.IsType<SetSalesApprovalAuthorityResult.MembershipUnavailable>(
                await SetAsync(services, administrator, member.MembershipId, 600m, "USD", 1)
            );
            Assert.IsType<RevokeSalesApprovalAuthorityResult.Revoked>(
                await RevokeAsync(services, administrator, member.MembershipId, 1)
            );
            OrganizationAccessContext rejoined = await InviteAndAcceptAsync(
                services,
                administrator,
                "member",
                [SalesRoleIds.Approver]
            );
            Assert.NotEqual(member.MembershipId, rejoined.MembershipId);
            Assert.IsType<GetSalesApprovalAuthorityResult.NotFound>(
                await GetAsync(services, administrator, rejoined.MembershipId)
            );
            Assert.IsType<SetSalesApprovalAuthorityResult.Saved>(
                await SetAsync(services, administrator, rejoined.MembershipId, 50m, "EUR", 0)
            );
            Assert.False(
                Assert
                    .IsType<GetSalesApprovalAuthorityResult.Found>(
                        await GetAsync(services, administrator, member.MembershipId)
                    )
                    .Authority.IsEnabled
            );
        }
        finally
        {
            foreach (IHostedService worker in workers.Reverse())
                await worker.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Theory]
    [InlineData("create")]
    [InlineData("replace")]
    [InlineData("revoke")]
    public async Task Administration_AuditPersistenceFails_RollsBackAuthorityChange(
        string operation
    )
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString()
        );
        OrganizationAccessContext manager = await CreateManagerAsync(services, "manager");
        SalesApprovalAuthorityView? original =
            operation == "create"
                ? null
                : Assert
                    .IsType<SetSalesApprovalAuthorityResult.Saved>(
                        await SetAsync(services, manager, manager.MembershipId, 100m, "USD", 0)
                    )
                    .Authority;
        await InstallAuditFailureAsync(services);
        if (operation == "revoke")
            await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() =>
                RevokeAsync(services, manager, manager.MembershipId, 1)
            );
        else
            await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() =>
                SetAsync(
                    services,
                    manager,
                    manager.MembershipId,
                    200m,
                    "EUR",
                    original?.Version ?? 0
                )
            );
        if (original is null)
        {
            Assert.IsType<GetSalesApprovalAuthorityResult.NotFound>(
                await GetAsync(services, manager, manager.MembershipId)
            );
            return;
        }
        Assert.Equal(
            original,
            Assert
                .IsType<GetSalesApprovalAuthorityResult.Found>(
                    await GetAsync(services, manager, manager.MembershipId)
                )
                .Authority
        );
    }

    [Fact]
    public async Task Administration_PermissionDenied_PersistsSecurityAuditBeforeReturning()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString()
        );
        OrganizationAccessContext manager = await CreateManagerAsync(services, "manager");
        await using (AsyncServiceScope scope = services.CreateAsyncScope())
        {
            Assert.IsType<ReplaceMembershipRolesResult.Updated>(
                await scope
                    .ServiceProvider.GetRequiredService<IOrganizationMembershipAdministration>()
                    .ReplaceRolesAsync(
                        new(
                            manager.UserId,
                            manager.OrganizationId,
                            manager.MembershipId,
                            [SystemRoleIds.OrganizationAdministrator]
                        ),
                        TestContext.Current.CancellationToken
                    )
            );
        }
        await InstallAuditFailureAsync(services);
        // An unavailable audit sink must not silently skip these security-significant denials.
        await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() =>
            SetAsync(services, manager, manager.MembershipId, 100m, "USD", 0)
        );
        await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() =>
            RevokeAsync(services, manager, manager.MembershipId, 1)
        );
    }

    [Fact]
    public async Task Revoke_CurrentVersion_RetainsDisabledAuthorityAndRejectsStaleRegrant()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString()
        );
        OrganizationAccessContext manager = await CreateManagerAsync(services, "manager");
        Assert.IsType<RevokeSalesApprovalAuthorityResult.InvalidExpectedVersion>(
            await RevokeAsync(services, manager, manager.MembershipId, 0)
        );
        Assert.IsType<RevokeSalesApprovalAuthorityResult.NotFound>(
            await RevokeAsync(services, manager, manager.MembershipId, 1)
        );
        Assert.IsType<SetSalesApprovalAuthorityResult.Saved>(
            await SetAsync(services, manager, manager.MembershipId, 100m, "USD", 0)
        );
        SalesApprovalAuthorityView revoked = Assert
            .IsType<RevokeSalesApprovalAuthorityResult.Revoked>(
                await RevokeAsync(services, manager, manager.MembershipId, 1)
            )
            .Authority;
        Assert.False(revoked.IsEnabled);
        Assert.Equal(2, revoked.Version);
        Assert.Equal(
            revoked,
            Assert
                .IsType<GetSalesApprovalAuthorityResult.Found>(
                    await GetAsync(services, manager, manager.MembershipId)
                )
                .Authority
        );
        Assert.IsType<RevokeSalesApprovalAuthorityResult.Unchanged>(
            await RevokeAsync(services, manager, manager.MembershipId, 2)
        );
        Assert.IsType<SetSalesApprovalAuthorityResult.VersionConflict>(
            await SetAsync(services, manager, manager.MembershipId, 100m, "USD", 0)
        );
        Assert.IsType<SetSalesApprovalAuthorityResult.VersionConflict>(
            await SetAsync(services, manager, manager.MembershipId, 100m, "USD", 1)
        );
        SalesApprovalAuthorityView regranted = Assert
            .IsType<SetSalesApprovalAuthorityResult.Saved>(
                await SetAsync(services, manager, manager.MembershipId, 200m, "EUR", 2)
            )
            .Authority;
        Assert.True(regranted.IsEnabled);
        Assert.Equal(3, regranted.Version);
        Assert.Equal("EUR", regranted.Currency);
        Assert.Equal(200m, regranted.MaximumAmount);
        Assert.IsType<RevokeSalesApprovalAuthorityResult.VersionConflict>(
            await RevokeAsync(services, manager, manager.MembershipId, 2)
        );
    }

    [Fact]
    public async Task Set_ActiveMembership_PersistsSalesOwnedLimitAndCurrency()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using ServiceProvider services = await CreateServicesAsync(
            postgres.GetConnectionString()
        );
        OrganizationAccessContext manager = await CreateManagerAsync(services, "manager");
        SalesApprovalAuthorityView authority = Assert
            .IsType<SetSalesApprovalAuthorityResult.Saved>(
                await SetAsync(services, manager, manager.MembershipId, 1000.25m, " usd ", 0)
            )
            .Authority;
        Assert.Equal(manager.MembershipId, authority.MembershipId);
        Assert.Equal("USD", authority.Currency);
        Assert.Equal(1000.25m, authority.MaximumAmount);
        Assert.True(authority.IsEnabled);
        Assert.Equal(1, authority.Version);
        Assert.Equal(
            authority,
            Assert
                .IsType<GetSalesApprovalAuthorityResult.Found>(
                    await GetAsync(services, manager, manager.MembershipId)
                )
                .Authority
        );
    }

    private static async Task<ServiceProvider> CreateServicesAsync(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddSingleton(NpgsqlDataSource.Create(connectionString));
        services.AddSingleton<TestOrganizationContextAccessor>();
        services.AddSingleton<IOrganizationContextAccessor>(provider =>
            provider.GetRequiredService<TestOrganizationContextAccessor>()
        );
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Invitations:PublicApplicationUrl"] = "https://example.test",
                    ["Email:Smtp:Host"] = "localhost",
                    ["Email:Smtp:Port"] = "1025",
                    ["Email:Smtp:Security"] = "None",
                    ["Email:Smtp:FromAddress"] = "no-reply@example.test",
                }
            )
            .Build();
        services.AddAccessModule(configuration);
        services.AddSingleton<RecordingEmailTransport>();
        services.Replace(
            ServiceDescriptor.Singleton<IEmailTransport>(provider =>
                provider.GetRequiredService<RecordingEmailTransport>()
            )
        );
        services.AddSalesModule();
        ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        await provider.MigrateAccessAsync(TestContext.Current.CancellationToken);
        await provider.MigrateSalesAsync(TestContext.Current.CancellationToken);
        return provider;
    }

    private static async Task<OrganizationAccessContext> CreateManagerAsync(
        ServiceProvider services,
        string slug
    )
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IServiceProvider provider = scope.ServiceProvider;
        UserIdentityLink user = await provider
            .GetRequiredService<IExternalIdentityLinking>()
            .LinkAsync(
                ExternalIdentity.Create(
                    "https://issuer.example",
                    slug,
                    slug + "@example.test",
                    slug
                ),
                TestContext.Current.CancellationToken
            );
        Assert.IsType<CreateOrganizationResult.Created>(
            await provider
                .GetRequiredService<IOrganizationCreation>()
                .CreateOrganizationAsync(
                    new(user.UserId, slug, slug),
                    TestContext.Current.CancellationToken
                )
        );
        OrganizationAccessContext context = (
            await provider
                .GetRequiredService<IOrganizationQueries>()
                .ResolveAccessAsync(user.UserId, slug, TestContext.Current.CancellationToken)
        )!;
        services.GetRequiredService<TestOrganizationContextAccessor>().OrganizationContext =
            context;
        Assert.IsType<ReplaceMembershipRolesResult.Updated>(
            await provider
                .GetRequiredService<IOrganizationMembershipAdministration>()
                .ReplaceRolesAsync(
                    new(
                        context.UserId,
                        context.OrganizationId,
                        context.MembershipId,
                        [SystemRoleIds.OrganizationAdministrator, SalesRoleIds.Manager]
                    ),
                    TestContext.Current.CancellationToken
                )
        );
        context = (
            await provider
                .GetRequiredService<IOrganizationQueries>()
                .ResolveAccessAsync(user.UserId, slug, TestContext.Current.CancellationToken)
        )!;
        services.GetRequiredService<TestOrganizationContextAccessor>().OrganizationContext =
            context;
        return context;
    }

    private static async Task<SetSalesApprovalAuthorityResult> SetAsync(
        ServiceProvider services,
        OrganizationAccessContext manager,
        MembershipId membershipId,
        decimal maximum,
        string currency,
        long expectedVersion
    )
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await scope
            .ServiceProvider.GetRequiredService<ISalesApprovalAuthorityAdministration>()
            .SetAsync(
                new(
                    manager.UserId,
                    manager.OrganizationId,
                    membershipId,
                    maximum,
                    currency,
                    expectedVersion
                ),
                TestContext.Current.CancellationToken
            );
    }

    private static async Task<GetSalesApprovalAuthorityResult> GetAsync(
        ServiceProvider services,
        OrganizationAccessContext manager,
        MembershipId membershipId
    )
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await scope
            .ServiceProvider.GetRequiredService<ISalesApprovalAuthorityAdministration>()
            .GetAsync(
                manager.UserId,
                manager.OrganizationId,
                membershipId,
                TestContext.Current.CancellationToken
            );
    }

    private static async Task<RevokeSalesApprovalAuthorityResult> RevokeAsync(
        ServiceProvider services,
        OrganizationAccessContext manager,
        MembershipId membershipId,
        long expectedVersion
    )
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await scope
            .ServiceProvider.GetRequiredService<ISalesApprovalAuthorityAdministration>()
            .RevokeAsync(
                new(manager.UserId, manager.OrganizationId, membershipId, expectedVersion),
                TestContext.Current.CancellationToken
            );
    }

    private sealed class TestOrganizationContextAccessor : IOrganizationContextAccessor
    {
        public OrganizationAccessContext? OrganizationContext { get; set; }
    }

    private static async Task InstallAuditFailureAsync(ServiceProvider services)
    {
        // SQL injects a storage fault, not a second interface for observing product data.
        await using NpgsqlConnection connection = await services
            .GetRequiredService<NpgsqlDataSource>()
            .OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            """
            CREATE FUNCTION sales.reject_authority_audit() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.action LIKE 'approval-authority.%' THEN RAISE EXCEPTION 'Injected audit failure'; END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER reject_authority_audit BEFORE INSERT ON sales.audit_entries
            FOR EACH ROW EXECUTE FUNCTION sales.reject_authority_audit();
            """,
            connection
        );
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task ChangeStatusAsync(
        ServiceProvider services,
        OrganizationAccessContext administrator,
        MembershipId membershipId,
        MembershipStatus status
    )
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        Assert.IsType<ChangeMembershipStatusResult.Changed>(
            await scope
                .ServiceProvider.GetRequiredService<IOrganizationMembershipAdministration>()
                .ChangeStatusAsync(
                    new(administrator.UserId, administrator.OrganizationId, membershipId, status),
                    TestContext.Current.CancellationToken
                )
        );
    }

    private static async Task<OrganizationAccessContext> InviteAndAcceptAsync(
        ServiceProvider services,
        OrganizationAccessContext administrator,
        string subject,
        IReadOnlyCollection<string> roles
    )
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        string email = subject + "@example.test";
        var invitations =
            scope.ServiceProvider.GetRequiredService<IOrganizationInvitationOperations>();
        InvitationId invitationId = Assert
            .IsType<CreateOrganizationInvitationResult.Created>(
                await invitations.CreateInvitationAsync(
                    new(administrator.UserId, administrator.OrganizationId, email, roles),
                    TestContext.Current.CancellationToken
                )
            )
            .Invitation.InvitationId;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken
        );
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        EmailMessage message = await services
            .GetRequiredService<RecordingEmailTransport>()
            .ReadAsync(timeout.Token);
        const string prefix = "Accept the invitation: ";
        var link = new Uri(
            message
                .TextBody.Split('\n', StringSplitOptions.TrimEntries)
                .Single(line => line.StartsWith(prefix, StringComparison.Ordinal))[prefix.Length..]
        );
        string secret = link
            .Query.TrimStart('?')
            .Split('&')
            .Select(part => part.Split('=', 2))
            .Where(part => part[0] == "code")
            .Select(part => Uri.UnescapeDataString(part[1]))
            .Single();
        UserIdentityLink user = await scope
            .ServiceProvider.GetRequiredService<IExternalIdentityLinking>()
            .LinkAsync(
                ExternalIdentity.Create("https://issuer.example", subject, email, subject),
                TestContext.Current.CancellationToken
            );
        Assert.IsType<AcceptOrganizationInvitationResult.Accepted>(
            await invitations.AcceptInvitationAsync(
                new(user.UserId, invitationId, secret, email),
                TestContext.Current.CancellationToken
            )
        );
        return (
            await scope
                .ServiceProvider.GetRequiredService<IOrganizationQueries>()
                .ResolveAccessAsync(
                    user.UserId,
                    administrator.OrganizationSlug,
                    TestContext.Current.CancellationToken
                )
        )!;
    }

    private sealed class RecordingEmailTransport : IEmailTransport
    {
        private readonly Channel<EmailMessage> _messages = Channel.CreateUnbounded<EmailMessage>();

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            _messages.Writer.TryWrite(message);
            return Task.CompletedTask;
        }

        internal Task<EmailMessage> ReadAsync(CancellationToken cancellationToken) =>
            _messages.Reader.ReadAsync(cancellationToken).AsTask();
    }
}
