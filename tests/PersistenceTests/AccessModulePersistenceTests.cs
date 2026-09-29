using System.Collections.Concurrent;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModulithFoundry.Modules.Access.Composition;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Access.ExtensionPoints;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Sales.Contracts;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ModulithFoundry.PersistenceTests;

public sealed class AccessModulePersistenceTests
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

    [Fact]
    public async Task ResolveOrganizationAccess_ActiveMember_ReturnsTheirOrganizationContext()
    {
        await using PostgreSqlContainer postgres = CreatePostgresContainer();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using ServiceProvider services = await CreateAccessServicesAsync(
            postgres.GetConnectionString());
        UserIdentityLink member = await LinkUserAsync(
            services,
            "member-subject",
            "member@example.test");
        UserIdentityLink otherUser = await LinkUserAsync(
            services,
            "other-subject",
            "other@example.test");
        OrganizationMembership organization = await CreateOrganizationAsync(
            services,
            member.UserId,
            "Scoped Organization",
            "scoped-organization");

        OrganizationAccessContext? resolved = await ResolveOrganizationAccessAsync(
            services,
            member.UserId,
            organization.Slug);
        OrganizationAccessContext? denied = await ResolveOrganizationAccessAsync(
            services,
            otherUser.UserId,
            organization.Slug);

        OrganizationAccessContext context = Assert.IsType<OrganizationAccessContext>(resolved);
        Assert.Equal(member.UserId, context.UserId);
        Assert.Equal(organization.OrganizationId, context.OrganizationId);
        Assert.NotEqual(Guid.Empty, context.MembershipId.Value);
        Assert.Equal(organization.Name, context.OrganizationName);
        Assert.Equal(organization.Slug, context.OrganizationSlug);
        Assert.Equal([SystemRoleIds.OrganizationAdministrator], context.RoleIds);
        Assert.Null(denied);
    }

    [Fact]
    public async Task CreateInvitation_ActiveAdministrator_CommitsInvitationAuditAndProtectedDelivery()
    {
        await using PostgreSqlContainer postgres = CreatePostgresContainer();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using ServiceProvider services = await CreateAccessServicesAsync(
            postgres.GetConnectionString());
        UserIdentityLink administrator = await LinkUserAsync(
            services,
            "inviting-administrator",
            "administrator@example.test");
        OrganizationMembership organization = await CreateOrganizationAsync(
            services,
            administrator.UserId,
            "Invitation Organization",
            "invitation-organization");

        CreateOrganizationInvitationResult result;
        await using (AsyncServiceScope scope = services.CreateAsyncScope())
        {
            result = await scope.ServiceProvider.GetRequiredService<IOrganizationInvitations>()
                .CreateInvitationAsync(
                    new CreateOrganizationInvitationCommand(
                        administrator.UserId,
                        organization.OrganizationId,
                        " Invited.Person@Example.Test ",
                        [SalesRoleIds.Clerk]),
                    TestContext.Current.CancellationToken);
        }

        CreateOrganizationInvitationResult.Created created =
            Assert.IsType<CreateOrganizationInvitationResult.Created>(result);
        Assert.Equal("invited.person@example.test", created.Invitation.RecipientEmail);
        Assert.Equal([SalesRoleIds.Clerk], created.Invitation.RoleIds);
        Assert.Equal(TimeSpan.FromDays(7), created.Invitation.ExpiresAt - created.Invitation.CreatedAt);

        InvitationStorageFacts stored = await ReadInvitationStorageFactsAsync(
            postgres.GetConnectionString(),
            created.Invitation.InvitationId);
        Assert.Equal(32, stored.SecretDigestLength);
        Assert.NotEmpty(stored.ProtectedPayload);
        Assert.DoesNotContain("invited.person@example.test", stored.ProtectedPayload, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("invitation.created", stored.AuditAction);
    }

    [Fact]
    public async Task ManageInvitation_InvalidConflictAndResend_EnforcesLifecycle()
    {
        await using PostgreSqlContainer postgres = CreatePostgresContainer();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        await using ServiceProvider services = await CreateAccessServicesAsync(
            postgres.GetConnectionString());
        UserIdentityLink administrator = await LinkUserAsync(
            services,
            "lifecycle-administrator",
            "administrator@example.test");
        UserIdentityLink nonmember = await LinkUserAsync(
            services,
            "lifecycle-nonmember",
            "nonmember@example.test");
        OrganizationMembership organization = await CreateOrganizationAsync(
            services,
            administrator.UserId,
            "Lifecycle Organization",
            "lifecycle-organization");

        CreateOrganizationInvitationResult invalidEmail = await CreateInvitationAsync(
            services,
            administrator.UserId,
            organization.OrganizationId,
            "not-an-email",
            [SalesRoleIds.Clerk]);
        CreateOrganizationInvitationResult invalidRole = await CreateInvitationAsync(
            services,
            administrator.UserId,
            organization.OrganizationId,
            "valid@example.test",
            ["unknown-role"]);
        CreateOrganizationInvitationResult existingMember = await CreateInvitationAsync(
            services,
            administrator.UserId,
            organization.OrganizationId,
            "administrator@example.test",
            [SalesRoleIds.Clerk]);
        CreateOrganizationInvitationResult denied = await CreateInvitationAsync(
            services,
            nonmember.UserId,
            organization.OrganizationId,
            "valid@example.test",
            [SalesRoleIds.Clerk]);
        CreateOrganizationInvitationResult created = await CreateInvitationAsync(
            services,
            administrator.UserId,
            organization.OrganizationId,
            "valid@example.test",
            [SalesRoleIds.Clerk]);
        OrganizationInvitation invitation = Assert.IsType<CreateOrganizationInvitationResult.Created>(created)
            .Invitation;
        CreateOrganizationInvitationResult duplicate = await CreateInvitationAsync(
            services,
            administrator.UserId,
            organization.OrganizationId,
            "VALID@example.test",
            [InventoryRoleIds.Manager]);
        ResendOrganizationInvitationResult resent = await ResendInvitationAsync(
            services,
            administrator.UserId,
            organization.OrganizationId,
            invitation.InvitationId);

        Assert.IsType<CreateOrganizationInvitationResult.InvalidEmail>(invalidEmail);
        Assert.IsType<CreateOrganizationInvitationResult.InvalidRoles>(invalidRole);
        Assert.IsType<CreateOrganizationInvitationResult.AlreadyMember>(existingMember);
        Assert.IsType<CreateOrganizationInvitationResult.PermissionDenied>(denied);
        Assert.Equal(
            invitation.InvitationId,
            Assert.IsType<CreateOrganizationInvitationResult.InvitationAlreadyPending>(duplicate)
                .Invitation.InvitationId);
        OrganizationInvitation resentInvitation =
            Assert.IsType<ResendOrganizationInvitationResult.Resent>(resent).Invitation;
        Assert.Equal(invitation.InvitationId, resentInvitation.InvitationId);
        Assert.True(resentInvitation.ExpiresAt >= invitation.ExpiresAt);

        InvitationDeliveryLifecycle lifecycle = await ReadInvitationDeliveryLifecycleAsync(
            postgres.GetConnectionString(),
            invitation.InvitationId);
        Assert.Equal(2, lifecycle.DeliveryCount);
        Assert.Equal(1, lifecycle.SupersededCount);
        Assert.Equal(1, lifecycle.PendingCount);
        Assert.Equal(1, lifecycle.ResentAuditCount);
    }

    [Fact]
    public async Task DeliverInvitation_TransientSmtpFailure_RetriesSameLinkAndClearsPayload()
    {
        await using PostgreSqlContainer postgres = CreatePostgresContainer();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        var transport = new FailOnceEmailTransport();
        await using ServiceProvider services = await CreateAccessServicesAsync(
            postgres.GetConnectionString(),
            collection => collection.AddSingleton<IEmailTransport>(transport));
        UserIdentityLink administrator = await LinkUserAsync(
            services,
            "delivery-administrator",
            "administrator@example.test");
        OrganizationMembership organization = await CreateOrganizationAsync(
            services,
            administrator.UserId,
            "Delivery Organization",
            "delivery-organization");
        CreateOrganizationInvitationResult created = await CreateInvitationAsync(
            services,
            administrator.UserId,
            organization.OrganizationId,
            "recipient@example.test",
            [SalesRoleIds.Clerk]);
        InvitationId invitationId = Assert.IsType<CreateOrganizationInvitationResult.Created>(created)
            .Invitation.InvitationId;

        IHostedService[] hostedServices = [.. services.GetServices<IHostedService>()];
        foreach (IHostedService hostedService in hostedServices)
        {
            await hostedService.StartAsync(TestContext.Current.CancellationToken);
        }

        try
        {
            await transport.WaitForSuccessfulRetryAsync(TestContext.Current.CancellationToken);
            await WaitForDeliveredAsync(
                postgres.GetConnectionString(),
                invitationId,
                TestContext.Current.CancellationToken);
        }
        finally
        {
            foreach (IHostedService hostedService in hostedServices.Reverse())
            {
                await hostedService.StopAsync(TestContext.Current.CancellationToken);
            }
        }

        EmailMessage[] attempts = transport.Attempts;
        Assert.Equal(2, attempts.Length);
        Assert.Equal(attempts[0].TextBody, attempts[1].TextBody);
        Assert.Contains("code=", attempts[0].TextBody, StringComparison.Ordinal);
        InvitationDeliveryState state = await ReadInvitationDeliveryStateAsync(
            postgres.GetConnectionString(),
            invitationId);
        Assert.Equal(1, state.AttemptCount);
        Assert.NotNull(state.SentAt);
        Assert.Null(state.ProtectedPayload);
    }

    [Fact]
    public async Task AcceptInvitation_MatchingVerifiedProviderEmail_CreatesMembershipWithAssignedRoles()
    {
        await using PostgreSqlContainer postgres = CreatePostgresContainer();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        var transport = new RecordingEmailTransport();
        await using ServiceProvider services = await CreateAccessServicesAsync(
            postgres.GetConnectionString(),
            collection => collection.AddSingleton<IEmailTransport>(transport));
        UserIdentityLink administrator = await LinkUserAsync(
            services,
            "accepting-administrator",
            "administrator@example.test");
        OrganizationMembership organization = await CreateOrganizationAsync(
            services,
            administrator.UserId,
            "Acceptance Organization",
            "acceptance-organization");
        DeliveredInvitation delivered = await CreateDeliveredInvitationAsync(
            services,
            transport,
            administrator.UserId,
            organization.OrganizationId,
            "recipient@example.test",
            [SalesRoleIds.Clerk, InventoryRoleIds.Manager]);
        UserIdentityLink recipient = await LinkUserAsync(
            services,
            "invitation-recipient",
            "recipient@example.test");

        AcceptOrganizationInvitationResult result = await AcceptInvitationAsync(
            services,
            recipient.UserId,
            delivered.InvitationId,
            delivered.Secret,
            "recipient@example.test");

        AcceptOrganizationInvitationResult.Accepted accepted =
            Assert.IsType<AcceptOrganizationInvitationResult.Accepted>(result);
        Assert.Equal(organization.OrganizationId, accepted.Membership.OrganizationId);
        Assert.Equal(
            [InventoryRoleIds.Manager, SalesRoleIds.Clerk],
            accepted.Membership.RoleIds);
        OrganizationMembership accessible = Assert.Single(
            await ListOrganizationsAsync(services, recipient.UserId));
        Assert.Equal(accepted.Membership.OrganizationId, accessible.OrganizationId);
        Assert.Equal(accepted.Membership.Name, accessible.Name);
        Assert.Equal(accepted.Membership.Slug, accessible.Slug);
        Assert.Equal(accepted.Membership.RoleIds, accessible.RoleIds);
        InvitationAcceptanceFacts facts = await ReadInvitationAcceptanceFactsAsync(
            postgres.GetConnectionString(),
            delivered.InvitationId);
        Assert.Equal("accepted", facts.Status);
        Assert.Equal(recipient.UserId.Value, facts.AcceptedByUserId);
        Assert.NotNull(facts.AcceptedAt);
        Assert.Equal("invitation.accepted", facts.AuditAction);
    }

    [Fact]
    public async Task AcceptInvitation_InvalidSecret_RejectsWithoutCreatingMembership()
    {
        await using PostgreSqlContainer postgres = CreatePostgresContainer();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        var transport = new RecordingEmailTransport();
        await using ServiceProvider services = await CreateAccessServicesAsync(
            postgres.GetConnectionString(),
            collection => collection.AddSingleton<IEmailTransport>(transport));
        UserIdentityLink administrator = await LinkUserAsync(
            services,
            "invalid-secret-administrator",
            "administrator@example.test");
        OrganizationMembership organization = await CreateOrganizationAsync(
            services,
            administrator.UserId,
            "Invalid Secret Organization",
            "invalid-secret-organization");
        DeliveredInvitation delivered = await CreateDeliveredInvitationAsync(
            services,
            transport,
            administrator.UserId,
            organization.OrganizationId,
            "recipient@example.test",
            [SalesRoleIds.Clerk]);
        UserIdentityLink recipient = await LinkUserAsync(
            services,
            "invalid-secret-recipient",
            "recipient@example.test");

        AcceptOrganizationInvitationResult result = await AcceptInvitationAsync(
            services,
            recipient.UserId,
            delivered.InvitationId,
            "not-the-invitation-secret",
            "recipient@example.test");

        Assert.IsType<AcceptOrganizationInvitationResult.Invalid>(result);
        Assert.Empty(await ListOrganizationsAsync(services, recipient.UserId));
    }

    [Fact]
    public async Task AcceptInvitation_DifferentVerifiedProviderEmail_RejectsWithoutCreatingMembership()
    {
        await using PostgreSqlContainer postgres = CreatePostgresContainer();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        var transport = new RecordingEmailTransport();
        await using ServiceProvider services = await CreateAccessServicesAsync(
            postgres.GetConnectionString(),
            collection => collection.AddSingleton<IEmailTransport>(transport));
        UserIdentityLink administrator = await LinkUserAsync(
            services,
            "mismatch-administrator",
            "administrator@example.test");
        OrganizationMembership organization = await CreateOrganizationAsync(
            services,
            administrator.UserId,
            "Mismatch Organization",
            "mismatch-organization");
        DeliveredInvitation delivered = await CreateDeliveredInvitationAsync(
            services,
            transport,
            administrator.UserId,
            organization.OrganizationId,
            "invited@example.test",
            [SalesRoleIds.Clerk]);
        UserIdentityLink differentUser = await LinkUserAsync(
            services,
            "different-recipient",
            "different@example.test");

        AcceptOrganizationInvitationResult result = await AcceptInvitationAsync(
            services,
            differentUser.UserId,
            delivered.InvitationId,
            delivered.Secret,
            "different@example.test");

        Assert.IsType<AcceptOrganizationInvitationResult.RecipientMismatch>(result);
        Assert.Empty(await ListOrganizationsAsync(services, differentUser.UserId));
    }

    [Fact]
    public async Task AcceptInvitation_ExpiredInvitation_RejectsWithoutCreatingMembership()
    {
        await using PostgreSqlContainer postgres = CreatePostgresContainer();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        var transport = new RecordingEmailTransport();
        var timeProvider = new AdjustableTimeProvider(
            new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero));
        await using ServiceProvider services = await CreateAccessServicesAsync(
            postgres.GetConnectionString(),
            collection =>
            {
                collection.AddSingleton<IEmailTransport>(transport);
                collection.AddSingleton<TimeProvider>(timeProvider);
            });
        UserIdentityLink administrator = await LinkUserAsync(
            services,
            "expiry-administrator",
            "administrator@example.test");
        OrganizationMembership organization = await CreateOrganizationAsync(
            services,
            administrator.UserId,
            "Expiry Organization",
            "expiry-organization");
        DeliveredInvitation delivered = await CreateDeliveredInvitationAsync(
            services,
            transport,
            administrator.UserId,
            organization.OrganizationId,
            "recipient@example.test",
            [SalesRoleIds.Clerk]);
        UserIdentityLink recipient = await LinkUserAsync(
            services,
            "expiry-recipient",
            "recipient@example.test");
        timeProvider.Advance(TimeSpan.FromDays(7));

        AcceptOrganizationInvitationResult result = await AcceptInvitationAsync(
            services,
            recipient.UserId,
            delivered.InvitationId,
            delivered.Secret,
            "recipient@example.test");

        Assert.IsType<AcceptOrganizationInvitationResult.Expired>(result);
        Assert.Empty(await ListOrganizationsAsync(services, recipient.UserId));
    }

    [Fact]
    public async Task AcceptInvitation_ReplayedByAcceptingUser_ReturnsExistingMembership()
    {
        await using PostgreSqlContainer postgres = CreatePostgresContainer();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        var transport = new RecordingEmailTransport();
        await using ServiceProvider services = await CreateAccessServicesAsync(
            postgres.GetConnectionString(),
            collection => collection.AddSingleton<IEmailTransport>(transport));
        UserIdentityLink administrator = await LinkUserAsync(
            services,
            "replay-administrator",
            "administrator@example.test");
        OrganizationMembership organization = await CreateOrganizationAsync(
            services,
            administrator.UserId,
            "Replay Organization",
            "replay-organization");
        DeliveredInvitation delivered = await CreateDeliveredInvitationAsync(
            services,
            transport,
            administrator.UserId,
            organization.OrganizationId,
            "recipient@example.test",
            [SalesRoleIds.Clerk]);
        UserIdentityLink recipient = await LinkUserAsync(
            services,
            "replay-recipient",
            "recipient@example.test");
        AcceptOrganizationInvitationResult first = await AcceptInvitationAsync(
            services,
            recipient.UserId,
            delivered.InvitationId,
            delivered.Secret,
            "recipient@example.test");

        AcceptOrganizationInvitationResult replay = await AcceptInvitationAsync(
            services,
            recipient.UserId,
            delivered.InvitationId,
            delivered.Secret,
            "recipient@example.test");

        Assert.IsType<AcceptOrganizationInvitationResult.Accepted>(first);
        Assert.IsType<AcceptOrganizationInvitationResult.AlreadyAccepted>(replay);
        Assert.Single(await ListOrganizationsAsync(services, recipient.UserId));
    }

    [Fact]
    public async Task AcceptInvitation_ReplayedByDifferentUser_ReturnsConsumed()
    {
        await using PostgreSqlContainer postgres = CreatePostgresContainer();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        var transport = new RecordingEmailTransport();
        await using ServiceProvider services = await CreateAccessServicesAsync(
            postgres.GetConnectionString(),
            collection => collection.AddSingleton<IEmailTransport>(transport));
        UserIdentityLink administrator = await LinkUserAsync(
            services,
            "consumed-administrator",
            "administrator@example.test");
        OrganizationMembership organization = await CreateOrganizationAsync(
            services,
            administrator.UserId,
            "Consumed Organization",
            "consumed-organization");
        DeliveredInvitation delivered = await CreateDeliveredInvitationAsync(
            services,
            transport,
            administrator.UserId,
            organization.OrganizationId,
            "recipient@example.test",
            [SalesRoleIds.Clerk]);
        UserIdentityLink firstRecipient = await LinkUserAsync(
            services,
            "first-consumed-recipient",
            "recipient@example.test");
        UserIdentityLink secondRecipient = await LinkUserAsync(
            services,
            "second-consumed-recipient",
            "recipient@example.test");
        await AcceptInvitationAsync(
            services,
            firstRecipient.UserId,
            delivered.InvitationId,
            delivered.Secret,
            "recipient@example.test");

        AcceptOrganizationInvitationResult replay = await AcceptInvitationAsync(
            services,
            secondRecipient.UserId,
            delivered.InvitationId,
            delivered.Secret,
            "recipient@example.test");

        Assert.IsType<AcceptOrganizationInvitationResult.Consumed>(replay);
        Assert.Empty(await ListOrganizationsAsync(services, secondRecipient.UserId));
    }

    [Fact]
    public async Task AcceptInvitation_ConcurrentDifferentUsers_CreatesOneMembership()
    {
        await using PostgreSqlContainer postgres = CreatePostgresContainer();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        var transport = new RecordingEmailTransport();
        await using ServiceProvider services = await CreateAccessServicesAsync(
            postgres.GetConnectionString(),
            collection => collection.AddSingleton<IEmailTransport>(transport));
        UserIdentityLink administrator = await LinkUserAsync(
            services,
            "concurrent-acceptance-administrator",
            "administrator@example.test");
        OrganizationMembership organization = await CreateOrganizationAsync(
            services,
            administrator.UserId,
            "Concurrent Acceptance Organization",
            "concurrent-acceptance-organization");
        DeliveredInvitation delivered = await CreateDeliveredInvitationAsync(
            services,
            transport,
            administrator.UserId,
            organization.OrganizationId,
            "recipient@example.test",
            [SalesRoleIds.Clerk]);
        UserIdentityLink firstRecipient = await LinkUserAsync(
            services,
            "first-concurrent-recipient",
            "recipient@example.test");
        UserIdentityLink secondRecipient = await LinkUserAsync(
            services,
            "second-concurrent-recipient",
            "recipient@example.test");

        AcceptOrganizationInvitationResult[] results = await Task.WhenAll(
            AcceptInvitationAsync(
                services,
                firstRecipient.UserId,
                delivered.InvitationId,
                delivered.Secret,
                "recipient@example.test"),
            AcceptInvitationAsync(
                services,
                secondRecipient.UserId,
                delivered.InvitationId,
                delivered.Secret,
                "recipient@example.test"));

        Assert.Single(results, result => result is AcceptOrganizationInvitationResult.Accepted);
        Assert.Single(results, result => result is AcceptOrganizationInvitationResult.Consumed);
        int membershipCount = (await ListOrganizationsAsync(services, firstRecipient.UserId)).Count
            + (await ListOrganizationsAsync(services, secondRecipient.UserId)).Count;
        Assert.Equal(1, membershipCount);
    }

    private static PostgreSqlContainer CreatePostgresContainer() =>
        new PostgreSqlBuilder("postgres:18.6")
            .Build();

    private static async Task<ServiceProvider> CreateAccessServicesAsync(
        string connectionString,
        Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddSingleton(NpgsqlDataSource.Create(connectionString));
        configure?.Invoke(services);
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
        services.AddAccessModule(
            configuration,
            [.. SalesRoleIds.All, .. InventoryRoleIds.All]);

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
        return await scope.ServiceProvider.GetRequiredService<IExternalIdentityLinking>()
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

    private static async Task<CreateOrganizationInvitationResult> CreateInvitationAsync(
        IServiceProvider services,
        UserId actorUserId,
        OrganizationId organizationId,
        string recipientEmail,
        IReadOnlyCollection<string> roleIds)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IOrganizationInvitations>()
            .CreateInvitationAsync(
                new CreateOrganizationInvitationCommand(
                    actorUserId,
                    organizationId,
                    recipientEmail,
                    roleIds),
                TestContext.Current.CancellationToken);
    }

    private static async Task<ResendOrganizationInvitationResult> ResendInvitationAsync(
        IServiceProvider services,
        UserId actorUserId,
        OrganizationId organizationId,
        InvitationId invitationId)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IOrganizationInvitations>()
            .ResendInvitationAsync(
                new ResendOrganizationInvitationCommand(
                    actorUserId,
                    organizationId,
                    invitationId),
                TestContext.Current.CancellationToken);
    }

    private static async Task<AcceptOrganizationInvitationResult> AcceptInvitationAsync(
        IServiceProvider services,
        UserId userId,
        InvitationId invitationId,
        string secret,
        string verifiedProviderEmail)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IOrganizationInvitations>()
            .AcceptInvitationAsync(
                new AcceptOrganizationInvitationCommand(
                    userId,
                    invitationId,
                    secret,
                    verifiedProviderEmail),
                TestContext.Current.CancellationToken);
    }

    private static async Task<DeliveredInvitation> CreateDeliveredInvitationAsync(
        IServiceProvider services,
        RecordingEmailTransport transport,
        UserId actorUserId,
        OrganizationId organizationId,
        string recipientEmail,
        IReadOnlyCollection<string> roleIds)
    {
        CreateOrganizationInvitationResult result = await CreateInvitationAsync(
            services,
            actorUserId,
            organizationId,
            recipientEmail,
            roleIds);
        InvitationId invitationId = Assert.IsType<CreateOrganizationInvitationResult.Created>(result)
            .Invitation.InvitationId;
        IHostedService[] hostedServices = [.. services.GetServices<IHostedService>()];
        foreach (IHostedService hostedService in hostedServices)
        {
            await hostedService.StartAsync(TestContext.Current.CancellationToken);
        }

        EmailMessage message;
        try
        {
            message = await transport.WaitForMessageAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            foreach (IHostedService hostedService in hostedServices.Reverse())
            {
                await hostedService.StopAsync(TestContext.Current.CancellationToken);
            }
        }

        const string linkPrefix = "Accept the invitation: ";
        string link = message.TextBody.Split('\n', StringSplitOptions.TrimEntries)
            .Single(line => line.StartsWith(linkPrefix, StringComparison.Ordinal))[linkPrefix.Length..];
        var uri = new Uri(link);
        Dictionary<string, string> query = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                part => Uri.UnescapeDataString(part[0]),
                part => Uri.UnescapeDataString(part[1]),
                StringComparer.Ordinal);
        Assert.Equal(invitationId.Value, Guid.Parse(query["invitationId"]));
        return new DeliveredInvitation(invitationId, query["code"]);
    }

    private static async Task<OrganizationAccessContext?> ResolveOrganizationAccessAsync(
        IServiceProvider services,
        UserId userId,
        string organizationSlug)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IOrganizationQueries>()
            .ResolveAccessAsync(
                userId,
                organizationSlug,
                TestContext.Current.CancellationToken);
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

    private static async Task<InvitationStorageFacts> ReadInvitationStorageFactsAsync(
        string connectionString,
        InvitationId invitationId)
    {
        const string sql = """
            SELECT octet_length(i.secret_digest),
                   d.protected_payload,
                   a.action
            FROM access.invitations AS i
            JOIN access.invitation_email_deliveries AS d
              ON d.invitation_id = i.id
             AND d.organization_id = i.organization_id
            JOIN access.audit_entries AS a
              ON a.subject_id = i.id
             AND a.organization_id = i.organization_id
            WHERE i.id = @invitation_id
            """;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("invitation_id", invitationId.Value);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(
            TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        var facts = new InvitationStorageFacts(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2));
        Assert.False(await reader.ReadAsync(TestContext.Current.CancellationToken));
        return facts;
    }

    private static async Task<InvitationDeliveryLifecycle> ReadInvitationDeliveryLifecycleAsync(
        string connectionString,
        InvitationId invitationId)
    {
        const string sql = """
            SELECT count(*),
                   count(*) FILTER (WHERE superseded_at IS NOT NULL),
                   count(*) FILTER (
                       WHERE superseded_at IS NULL
                         AND sent_at IS NULL
                         AND protected_payload IS NOT NULL)
            FROM access.invitation_email_deliveries
            WHERE invitation_id = @invitation_id;

            SELECT count(*)
            FROM access.audit_entries
            WHERE subject_id = @invitation_id
              AND action = 'invitation.resent';
            """;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("invitation_id", invitationId.Value);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(
            TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        long deliveryCount = reader.GetInt64(0);
        long supersededCount = reader.GetInt64(1);
        long pendingCount = reader.GetInt64(2);
        Assert.True(await reader.NextResultAsync(TestContext.Current.CancellationToken));
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        return new InvitationDeliveryLifecycle(
            deliveryCount,
            supersededCount,
            pendingCount,
            reader.GetInt64(0));
    }

    private static async Task<InvitationAcceptanceFacts> ReadInvitationAcceptanceFactsAsync(
        string connectionString,
        InvitationId invitationId)
    {
        const string sql = """
            SELECT i.status,
                   i.accepted_by_user_id,
                   i.accepted_at,
                   a.action
            FROM access.invitations AS i
            LEFT JOIN access.audit_entries AS a
              ON a.organization_id = i.organization_id
             AND a.subject_id = i.id
             AND a.action = 'invitation.accepted'
            WHERE i.id = @invitation_id
            """;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("invitation_id", invitationId.Value);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(
            TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        return new InvitationAcceptanceFacts(
            reader.GetString(0),
            reader.GetGuid(1),
            reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2),
            reader.IsDBNull(3) ? null : reader.GetString(3));
    }

    private static async Task WaitForDeliveredAsync(
        string connectionString,
        InvitationId invitationId,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            InvitationDeliveryState state = await ReadInvitationDeliveryStateAsync(
                connectionString,
                invitationId);
            if (state.SentAt is not null)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }
    }

    private static async Task<InvitationDeliveryState> ReadInvitationDeliveryStateAsync(
        string connectionString,
        InvitationId invitationId)
    {
        const string sql = """
            SELECT attempt_count, sent_at, protected_payload
            FROM access.invitation_email_deliveries
            WHERE invitation_id = @invitation_id
              AND superseded_at IS NULL
            """;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("invitation_id", invitationId.Value);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(
            TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        return new InvitationDeliveryState(
            reader.GetInt32(0),
            reader.IsDBNull(1) ? null : reader.GetFieldValue<DateTimeOffset>(1),
            reader.IsDBNull(2) ? null : reader.GetString(2));
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

    private sealed record InvitationStorageFacts(
        int SecretDigestLength,
        string ProtectedPayload,
        string AuditAction);

    private sealed record InvitationDeliveryLifecycle(
        long DeliveryCount,
        long SupersededCount,
        long PendingCount,
        long ResentAuditCount);

    private sealed record InvitationDeliveryState(
        int AttemptCount,
        DateTimeOffset? SentAt,
        string? ProtectedPayload);

    private sealed record InvitationAcceptanceFacts(
        string Status,
        Guid AcceptedByUserId,
        DateTimeOffset? AcceptedAt,
        string? AuditAction);

    private sealed record DeliveredInvitation(
        InvitationId InvitationId,
        string Secret);

    private sealed class RecordingEmailTransport : IEmailTransport
    {
        private readonly TaskCompletionSource<EmailMessage> _message = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            _message.TrySetResult(message);
            return Task.CompletedTask;
        }

        internal Task<EmailMessage> WaitForMessageAsync(CancellationToken cancellationToken) =>
            _message.Task.WaitAsync(cancellationToken);
    }

    private sealed class AdjustableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        internal void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }

    private sealed class FailOnceEmailTransport : IEmailTransport
    {
        private readonly ConcurrentQueue<EmailMessage> _attempts = new();
        private readonly TaskCompletionSource _successfulRetry = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _attemptCount;

        internal EmailMessage[] Attempts => [.. _attempts];

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            _attempts.Enqueue(message);
            if (Interlocked.Increment(ref _attemptCount) == 1)
            {
                throw new InvalidOperationException("Simulated ambiguous SMTP failure.");
            }

            _successfulRetry.TrySetResult();
            return Task.CompletedTask;
        }

        internal Task WaitForSuccessfulRetryAsync(CancellationToken cancellationToken) =>
            _successfulRetry.Task.WaitAsync(cancellationToken);
    }
}
