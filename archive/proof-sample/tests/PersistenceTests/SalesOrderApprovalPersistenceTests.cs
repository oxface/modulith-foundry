using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Sales.Contracts;
using Npgsql;

namespace ModulithFoundry.PersistenceTests;

public sealed class SalesOrderApprovalPersistenceTests
{
    [Fact]
    public async Task Approve_CancelledBeforeCommit_RollsBackAndReleasesBothGuards()
    {
        await using SalesApprovalFixture fixture = await SalesApprovalFixture.CreateAsync();
        SalesOrderView order = await fixture.CreateOrderAsync();
        Assert.IsType<SetSalesApprovalAuthorityResult.Saved>(await fixture.GrantAsync());
        await InstallApprovalPauseAsync(fixture);
        await using NpgsqlConnection controller = await fixture.OpenConnectionAsync();
        await using NpgsqlTransaction hold = await controller.BeginTransactionAsync(
            TestContext.Current.CancellationToken
        );
        await ExecuteAsync(controller, "SELECT pg_advisory_xact_lock(704201)");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken
        );
        Task<ApproveSalesOrderResult> approval = ApproveAsync(
            fixture,
            order.OrderNumber,
            2,
            cancellationToken: cancellation.Token
        );
        try
        {
            await WaitForLockAsync(fixture, "advisory", approval);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await approval);
        }
        finally
        {
            await hold.RollbackAsync(CancellationToken.None);
        }
        // These real writes prove no abandoned Access guard or Sales authority lock remains.
        AssertSuccessfulRevocation(
            "authority",
            await RevokeAsync(fixture, "authority")
                .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken)
        );
        AssertSuccessfulRevocation(
            "role",
            await RevokeAsync(fixture, "role")
                .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken)
        );
        Assert.IsType<GetOrderFulfilmentResult.NotFound>(
            await GetProcessAsync(fixture, order.OrderNumber, fixture.Administrator)
        );
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("disabled")]
    [InlineData("currency")]
    [InlineData("limit")]
    public async Task Approve_AuthorityDoesNotCoverOrder_RejectsWithoutBusinessChanges(
        string scenario
    )
    {
        await using SalesApprovalFixture fixture = await SalesApprovalFixture.CreateAsync();
        SalesOrderView order = await fixture.CreateOrderAsync();
        if (scenario != "missing")
            Assert.IsType<SetSalesApprovalAuthorityResult.Saved>(
                await fixture.GrantAsync(
                    scenario == "limit" ? 49.99m : 50m,
                    scenario == "currency" ? "EUR" : "USD"
                )
            );
        if (scenario == "disabled")
            AssertSuccessfulRevocation("authority", await RevokeAsync(fixture, "authority"));
        ApproveSalesOrderResult rejected = await ApproveAsync(
            fixture,
            order.OrderNumber,
            order.Version
        );
        switch (scenario)
        {
            case "missing":
            case "disabled":
                Assert.IsType<ApproveSalesOrderResult.AuthorityUnavailable>(rejected);
                break;
            case "currency":
                Assert.IsType<ApproveSalesOrderResult.CurrencyMismatch>(rejected);
                break;
            case "limit":
                Assert.IsType<ApproveSalesOrderResult.LimitExceeded>(rejected);
                break;
        }
        Assert.IsType<GetOrderFulfilmentResult.NotFound>(
            await GetProcessAsync(fixture, order.OrderNumber)
        );
        var activity = Assert.IsType<GetSalesOrderActivityResult.Found>(
            await fixture.RunAsync(
                fixture.Approver,
                provider =>
                    provider
                        .GetRequiredService<ISalesOrderOperations>()
                        .GetActivityAsync(
                            fixture.Approver.UserId,
                            fixture.Approver.OrganizationId,
                            order.OrderNumber,
                            TestContext.Current.CancellationToken
                        )
            )
        );
        Assert.Equal(2, activity.Entries.Count);
    }

    [Fact]
    public async Task Approve_InvalidStateVersionOrMissingOrder_DoesNotStartAnotherProcess()
    {
        await using SalesApprovalFixture fixture = await SalesApprovalFixture.CreateAsync();
        SalesOrderView draft = await fixture.CreateOrderAsync(submit: false);
        Assert.IsType<SetSalesApprovalAuthorityResult.Saved>(await fixture.GrantAsync());
        Assert.IsType<ApproveSalesOrderResult.InvalidExpectedVersion>(
            await ApproveAsync(fixture, draft.OrderNumber, 0)
        );
        Assert.IsType<ApproveSalesOrderResult.NotFound>(await ApproveAsync(fixture, 999, 1));
        Assert.IsType<ApproveSalesOrderResult.NotAwaitingApproval>(
            await ApproveAsync(fixture, draft.OrderNumber, draft.Version)
        );
        Assert.IsType<GetOrderFulfilmentResult.NotFound>(
            await GetProcessAsync(fixture, draft.OrderNumber)
        );
        SalesOrderView order = await fixture.CreateOrderAsync();
        Assert.IsType<ApproveSalesOrderResult.VersionConflict>(
            await ApproveAsync(fixture, order.OrderNumber, 1)
        );
        Assert.IsType<ApproveSalesOrderResult.Approved>(
            await ApproveAsync(fixture, order.OrderNumber, order.Version)
        );
        OrderFulfilmentView first = Assert
            .IsType<GetOrderFulfilmentResult.Found>(
                await GetProcessAsync(fixture, order.OrderNumber)
            )
            .Process;
        Assert.IsType<ApproveSalesOrderResult.VersionConflict>(
            await ApproveAsync(fixture, order.OrderNumber, 2)
        );
        Assert.IsType<ApproveSalesOrderResult.NotAwaitingApproval>(
            await ApproveAsync(fixture, order.OrderNumber, 3)
        );
        Assert.Equivalent(
            first,
            Assert
                .IsType<GetOrderFulfilmentResult.Found>(
                    await GetProcessAsync(fixture, order.OrderNumber)
                )
                .Process,
            strict: true
        );
    }

    [Fact]
    public async Task Approve_CompetingApprovals_CommitsOneTransitionActivityAndProcess()
    {
        await using SalesApprovalFixture fixture = await SalesApprovalFixture.CreateAsync();
        SalesOrderView order = await fixture.CreateOrderAsync();
        Assert.IsType<SetSalesApprovalAuthorityResult.Saved>(await fixture.GrantAsync());
        ApproveSalesOrderResult[] results = await Task.WhenAll(
            ApproveAsync(fixture, order.OrderNumber, 2),
            ApproveAsync(fixture, order.OrderNumber, 2)
        );
        Assert.Single(results.OfType<ApproveSalesOrderResult.Approved>());
        Assert.Single(results.OfType<ApproveSalesOrderResult.VersionConflict>());
        Assert.IsType<GetOrderFulfilmentResult.Found>(
            await GetProcessAsync(fixture, order.OrderNumber)
        );
        var activity = Assert.IsType<GetSalesOrderActivityResult.Found>(
            await fixture.RunAsync(
                fixture.Approver,
                provider =>
                    provider
                        .GetRequiredService<ISalesOrderOperations>()
                        .GetActivityAsync(
                            fixture.Approver.UserId,
                            fixture.Approver.OrganizationId,
                            order.OrderNumber,
                            TestContext.Current.CancellationToken
                        )
            )
        );
        Assert.Equal(3, activity.Entries.Count);
        Assert.Single(activity.Entries, entry => entry.Kind == SalesOrderActivityKind.Approved);
    }

    [Theory]
    [InlineData("audit_entries", "NEW.action = 'sales-order.approved'")]
    [InlineData("order_activity", "NEW.kind = 'approved'")]
    [InlineData("fulfilment_processes", "true")]
    public async Task Approve_AtomicParticipantFails_RollsBackAllChangesAndAllowsFreshRetry(
        string table,
        string predicate
    )
    {
        await using SalesApprovalFixture fixture = await SalesApprovalFixture.CreateAsync();
        SalesOrderView order = await fixture.CreateOrderAsync();
        Assert.IsType<SetSalesApprovalAuthorityResult.Saved>(await fixture.GrantAsync());
        await using NpgsqlConnection connection = await fixture.OpenConnectionAsync();
        await ExecuteAsync(
            connection,
            $$"""
            CREATE FUNCTION sales.reject_approval() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF {{predicate}} THEN RAISE EXCEPTION 'Injected approval write failure'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER reject_approval BEFORE INSERT ON sales.{{table}}
            FOR EACH ROW EXECUTE FUNCTION sales.reject_approval();
            """
        );
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            ApproveAsync(fixture, order.OrderNumber, 2)
        );
        Assert.IsType<GetOrderFulfilmentResult.NotFound>(
            await GetProcessAsync(fixture, order.OrderNumber)
        );
        SalesOrderView unchanged = Assert
            .IsType<GetSalesOrderResult.Found>(
                await fixture.RunAsync(
                    fixture.Approver,
                    provider =>
                        provider
                            .GetRequiredService<ISalesOrderOperations>()
                            .GetByNumberAsync(
                                fixture.Approver.UserId,
                                fixture.Approver.OrganizationId,
                                order.OrderNumber,
                                TestContext.Current.CancellationToken
                            )
                )
            )
            .Order;
        Assert.Equal(2, unchanged.Version);
        Assert.Equal(SalesOrderStatus.AwaitingApproval, unchanged.Status);
        Assert.Null(unchanged.ApprovedBy);
        var activity = Assert.IsType<GetSalesOrderActivityResult.Found>(
            await fixture.RunAsync(
                fixture.Approver,
                provider =>
                    provider
                        .GetRequiredService<ISalesOrderOperations>()
                        .GetActivityAsync(
                            fixture.Approver.UserId,
                            fixture.Approver.OrganizationId,
                            order.OrderNumber,
                            TestContext.Current.CancellationToken
                        )
            )
        );
        Assert.Equal(2, activity.Entries.Count);
        await ExecuteAsync(connection, $"DROP TRIGGER reject_approval ON sales.{table}");
        Assert.IsType<ApproveSalesOrderResult.Approved>(
            await ApproveAsync(fixture, order.OrderNumber, 2)
        );
    }

    [Fact]
    public async Task Approve_ForeignUnresolvedOrForgedContext_FailsClosed()
    {
        await using SalesApprovalFixture fixture = await SalesApprovalFixture.CreateAsync();
        SalesOrderView order = await fixture.CreateOrderAsync();
        Assert.IsType<SetSalesApprovalAuthorityResult.Saved>(await fixture.GrantAsync());
        Assert.IsType<ApproveSalesOrderResult.PermissionDenied>(
            await fixture.RunAsync(
                null,
                provider =>
                    provider
                        .GetRequiredService<ISalesOrderApproval>()
                        .ApproveAsync(
                            new(
                                fixture.Approver.UserId,
                                fixture.Approver.OrganizationId,
                                order.OrderNumber,
                                2
                            ),
                            TestContext.Current.CancellationToken
                        )
            )
        );
        Assert.IsType<ApproveSalesOrderResult.PermissionDenied>(
            await fixture.RunAsync(
                fixture.Approver,
                provider =>
                    provider
                        .GetRequiredService<ISalesOrderApproval>()
                        .ApproveAsync(
                            new(
                                fixture.Administrator.UserId,
                                fixture.Administrator.OrganizationId,
                                order.OrderNumber,
                                2
                            ),
                            TestContext.Current.CancellationToken
                        )
            )
        );
        Assert.IsType<ApproveSalesOrderResult.PermissionDenied>(
            await ApproveAsync(
                fixture,
                order.OrderNumber,
                2,
                fixture.Approver with
                {
                    MembershipId = fixture.Administrator.MembershipId,
                }
            )
        );
        OrganizationAccessContext other = await fixture.CreateOrganizationAsync("other");
        await fixture.RunAsync(
            other,
            async provider =>
                Assert.IsType<ReplaceMembershipRolesResult.Updated>(
                    await provider
                        .GetRequiredService<IOrganizationMembershipAdministration>()
                        .ReplaceRolesAsync(
                            new(
                                other.UserId,
                                other.OrganizationId,
                                other.MembershipId,
                                [SystemRoleIds.OrganizationAdministrator, SalesRoleIds.Approver]
                            ),
                            TestContext.Current.CancellationToken
                        )
                )
        );
        Assert.IsType<ApproveSalesOrderResult.NotFound>(
            await ApproveAsync(fixture, order.OrderNumber, 2, other)
        );
        Assert.IsType<GetOrderFulfilmentResult.NotFound>(
            await GetProcessAsync(fixture, order.OrderNumber, other)
        );
        Assert.IsType<GetOrderFulfilmentResult.PermissionDenied>(
            await fixture.RunAsync(
                null,
                provider =>
                    provider
                        .GetRequiredService<ISalesOrderOperations>()
                        .GetFulfilmentAsync(
                            fixture.Approver.UserId,
                            fixture.Approver.OrganizationId,
                            order.OrderNumber,
                            TestContext.Current.CancellationToken
                        )
            )
        );
    }

    [Fact]
    public async Task Approve_RemovedUserRejoins_RequiresAuthorityForNewTenure()
    {
        await using SalesApprovalFixture fixture = await SalesApprovalFixture.CreateAsync();
        Assert.IsType<SetSalesApprovalAuthorityResult.Saved>(await fixture.GrantAsync());
        await fixture.RunAsync(
            fixture.Administrator,
            async provider =>
                Assert.IsType<ChangeMembershipStatusResult.Changed>(
                    await provider
                        .GetRequiredService<IOrganizationMembershipAdministration>()
                        .ChangeStatusAsync(
                            new(
                                fixture.Administrator.UserId,
                                fixture.Administrator.OrganizationId,
                                fixture.Approver.MembershipId,
                                MembershipStatus.Removed
                            ),
                            TestContext.Current.CancellationToken
                        )
                )
        );
        OrganizationAccessContext rejoined = await fixture.InviteAsync(
            "approver",
            [SalesRoleIds.Approver]
        );
        Assert.Equal(fixture.Approver.UserId, rejoined.UserId);
        Assert.NotEqual(fixture.Approver.MembershipId, rejoined.MembershipId);
        SalesOrderView order = await fixture.CreateOrderAsync();
        Assert.IsType<ApproveSalesOrderResult.PermissionDenied>(
            await ApproveAsync(fixture, order.OrderNumber, 2)
        );
        Assert.IsType<ApproveSalesOrderResult.AuthorityUnavailable>(
            await ApproveAsync(fixture, order.OrderNumber, 2, rejoined)
        );
        Assert.IsType<SetSalesApprovalAuthorityResult.Saved>(
            await fixture.GrantAsync(target: rejoined)
        );
        Assert.IsType<ApproveSalesOrderResult.Approved>(
            await ApproveAsync(fixture, order.OrderNumber, 2, rejoined)
        );
    }

    [Fact]
    public async Task Approve_SelfApprovalDenialCannotBeAudited_FailsWithoutChangingOrder()
    {
        await using SalesApprovalFixture fixture = await SalesApprovalFixture.CreateAsync();
        await fixture.RunAsync(
            fixture.Administrator,
            async provider =>
                Assert.IsType<ReplaceMembershipRolesResult.Updated>(
                    await provider
                        .GetRequiredService<IOrganizationMembershipAdministration>()
                        .ReplaceRolesAsync(
                            new(
                                fixture.Administrator.UserId,
                                fixture.Administrator.OrganizationId,
                                fixture.Administrator.MembershipId,
                                [
                                    SystemRoleIds.OrganizationAdministrator,
                                    SalesRoleIds.Clerk,
                                    SalesRoleIds.Manager,
                                    SalesRoleIds.Approver,
                                ]
                            ),
                            TestContext.Current.CancellationToken
                        )
                )
        );
        SalesOrderView order = await fixture.CreateOrderAsync();
        Assert.IsType<SetSalesApprovalAuthorityResult.Saved>(
            await fixture.GrantAsync(target: fixture.Administrator)
        );
        await using NpgsqlConnection connection = await fixture.OpenConnectionAsync();
        await ExecuteAsync(
            connection,
            """
            CREATE FUNCTION sales.reject_denial() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.action = 'sales-order.approve-denied' THEN RAISE EXCEPTION 'Injected denial audit fault'; END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER reject_denial BEFORE INSERT ON sales.audit_entries
            FOR EACH ROW EXECUTE FUNCTION sales.reject_denial();
            """
        );
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            ApproveAsync(fixture, order.OrderNumber, order.Version, fixture.Administrator)
        );
        Assert.IsType<GetOrderFulfilmentResult.NotFound>(
            await GetProcessAsync(fixture, order.OrderNumber, fixture.Administrator)
        );
        await ExecuteAsync(connection, "DROP TRIGGER reject_denial ON sales.audit_entries");
        Assert.IsType<ApproveSalesOrderResult.SelfApprovalDenied>(
            await ApproveAsync(fixture, order.OrderNumber, order.Version, fixture.Administrator)
        );
    }

    [Theory]
    [InlineData("authority")]
    [InlineData("role")]
    [InlineData("membership")]
    public async Task Approve_RevocationWinsFirst_RechecksAfterWaitingAndCreatesNoProcess(
        string kind
    )
    {
        await using SalesApprovalFixture fixture = await SalesApprovalFixture.CreateAsync();
        SalesOrderView order = await fixture.CreateOrderAsync();
        Assert.IsType<SetSalesApprovalAuthorityResult.Saved>(await fixture.GrantAsync());
        await using NpgsqlConnection setup = await fixture.OpenConnectionAsync();
        await ExecuteAsync(
            setup,
            kind == "authority"
                ? """
                CREATE FUNCTION sales.pause_revoke() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN PERFORM pg_advisory_xact_lock(704201); RETURN NEW; END $$;
                CREATE TRIGGER pause_revoke AFTER UPDATE ON sales.approval_authorities
                FOR EACH ROW EXECUTE FUNCTION sales.pause_revoke();
                """
                : """
                CREATE FUNCTION access.pause_revoke() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN PERFORM pg_advisory_xact_lock(704201); RETURN NEW; END $$;
                CREATE TRIGGER pause_revoke BEFORE INSERT ON access.audit_entries
                FOR EACH ROW EXECUTE FUNCTION access.pause_revoke();
                """
        );
        await using NpgsqlConnection controller = await fixture.OpenConnectionAsync();
        await using NpgsqlTransaction hold = await controller.BeginTransactionAsync(
            TestContext.Current.CancellationToken
        );
        await ExecuteAsync(controller, "SELECT pg_advisory_xact_lock(704201)");
        Task<object> revoke = RevokeAsync(fixture, kind);
        Task<ApproveSalesOrderResult>? approval = null;
        try
        {
            await WaitForLockAsync(fixture, "advisory", revoke);
            approval = ApproveAsync(fixture, order.OrderNumber, order.Version);
            await WaitForLockAsync(fixture, "row", approval);
            Assert.False(approval.IsCompleted);
        }
        finally
        {
            await hold.RollbackAsync(CancellationToken.None);
            await revoke;
            if (approval is not null)
                await approval;
        }
        AssertSuccessfulRevocation(kind, await revoke);
        if (kind == "authority")
            Assert.IsType<ApproveSalesOrderResult.AuthorityUnavailable>(await approval!);
        else
            Assert.IsType<ApproveSalesOrderResult.PermissionDenied>(await approval!);
        Assert.IsType<GetOrderFulfilmentResult.NotFound>(
            await GetProcessAsync(fixture, order.OrderNumber, fixture.Administrator)
        );
        SalesOrderView unchanged = Assert
            .IsType<GetSalesOrderResult.Found>(
                await fixture.RunAsync(
                    fixture.Administrator,
                    provider =>
                        provider
                            .GetRequiredService<ISalesOrderOperations>()
                            .GetByNumberAsync(
                                fixture.Administrator.UserId,
                                fixture.Administrator.OrganizationId,
                                order.OrderNumber,
                                TestContext.Current.CancellationToken
                            )
                )
            )
            .Order;
        Assert.Equal(SalesOrderStatus.AwaitingApproval, unchanged.Status);
        Assert.Equal(2, unchanged.Version);
    }

    [Theory]
    [InlineData("authority")]
    [InlineData("role")]
    [InlineData("membership")]
    public async Task Approve_RevocationRacesCommit_RevocationWaitsForAcceptedApproval(string kind)
    {
        await using SalesApprovalFixture fixture = await SalesApprovalFixture.CreateAsync();
        SalesOrderView order = await fixture.CreateOrderAsync();
        Assert.IsType<SetSalesApprovalAuthorityResult.Saved>(await fixture.GrantAsync());
        await InstallApprovalPauseAsync(fixture);
        await using NpgsqlConnection controller = await fixture.OpenConnectionAsync();
        await using NpgsqlTransaction hold = await controller.BeginTransactionAsync(
            TestContext.Current.CancellationToken
        );
        await ExecuteAsync(controller, "SELECT pg_advisory_xact_lock(704201)");
        Task<ApproveSalesOrderResult> approval = ApproveAsync(
            fixture,
            order.OrderNumber,
            order.Version
        );
        Task<object>? revoke = null;
        try
        {
            await WaitForLockAsync(fixture, "advisory", approval);
            revoke = RevokeAsync(fixture, kind);
            await WaitForLockAsync(fixture, "row", revoke);
            Assert.False(revoke.IsCompleted);
        }
        finally
        {
            await hold.RollbackAsync(CancellationToken.None);
            await approval;
            if (revoke is not null)
                await revoke;
        }
        Assert.IsType<ApproveSalesOrderResult.Approved>(await approval);
        AssertSuccessfulRevocation(kind, await revoke!);
        Assert.IsType<GetOrderFulfilmentResult.Found>(
            await GetProcessAsync(fixture, order.OrderNumber, fixture.Administrator)
        );
        SalesOrderView later = await fixture.CreateOrderAsync();
        ApproveSalesOrderResult denied = await ApproveAsync(
            fixture,
            later.OrderNumber,
            later.Version
        );
        if (kind == "authority")
            Assert.IsType<ApproveSalesOrderResult.AuthorityUnavailable>(denied);
        else
            Assert.IsType<ApproveSalesOrderResult.PermissionDenied>(denied);
    }

    [Fact]
    public async Task Approve_IndependentAuthorizedMember_CommitsApprovalActivityAndPendingProcess()
    {
        await using SalesApprovalFixture fixture = await SalesApprovalFixture.CreateAsync();
        SalesOrderView submitted = await fixture.CreateOrderAsync();
        Assert.IsType<SetSalesApprovalAuthorityResult.Saved>(await fixture.GrantAsync());
        SalesOrderView approved = Assert
            .IsType<ApproveSalesOrderResult.Approved>(
                await ApproveAsync(fixture, submitted.OrderNumber, submitted.Version)
            )
            .Order;
        Assert.Equal(SalesOrderStatus.Approved, approved.Status);
        Assert.Equal(3, approved.Version);
        Assert.Equal(fixture.Approver.UserId, approved.ApprovedBy);
        Assert.NotNull(approved.ApprovedAt);
        SalesOrderView read = Assert
            .IsType<GetSalesOrderResult.Found>(
                await fixture.RunAsync(
                    fixture.Approver,
                    provider =>
                        provider
                            .GetRequiredService<ISalesOrderOperations>()
                            .GetByNumberAsync(
                                fixture.Approver.UserId,
                                fixture.Approver.OrganizationId,
                                submitted.OrderNumber,
                                TestContext.Current.CancellationToken
                            )
                )
            )
            .Order;
        Assert.Equal(approved.ApprovedAt, read.ApprovedAt);
        Assert.Equal(approved.ApprovedBy, read.ApprovedBy);
        OrderFulfilmentView process = Assert
            .IsType<GetOrderFulfilmentResult.Found>(
                await GetProcessAsync(fixture, submitted.OrderNumber)
            )
            .Process;
        Assert.Equal(OrderFulfilmentStatus.PendingDispatch, process.Status);
        Assert.Equal(approved.ApprovedAt, process.CreatedAt);
        Assert.Equal(submitted.OrderNumber, process.OrderNumber);
        var activity = Assert.IsType<GetSalesOrderActivityResult.Found>(
            await fixture.RunAsync(
                fixture.Approver,
                provider =>
                    provider
                        .GetRequiredService<ISalesOrderOperations>()
                        .GetActivityAsync(
                            fixture.Approver.UserId,
                            fixture.Approver.OrganizationId,
                            submitted.OrderNumber,
                            TestContext.Current.CancellationToken
                        )
            )
        );
        Assert.Equal(
            [
                SalesOrderActivityKind.Created,
                SalesOrderActivityKind.Submitted,
                SalesOrderActivityKind.Approved,
            ],
            activity.Entries.Select(entry => entry.Kind)
        );
    }

    private static Task<ApproveSalesOrderResult> ApproveAsync(
        SalesApprovalFixture fixture,
        long number,
        long version,
        OrganizationAccessContext? actor = null,
        CancellationToken? cancellationToken = null
    )
    {
        actor ??= fixture.Approver;
        return fixture.RunAsync(
            actor,
            provider =>
                provider
                    .GetRequiredService<ISalesOrderApproval>()
                    .ApproveAsync(
                        new(actor.UserId, actor.OrganizationId, number, version),
                        cancellationToken ?? TestContext.Current.CancellationToken
                    )
        );
    }

    private static Task<GetOrderFulfilmentResult> GetProcessAsync(
        SalesApprovalFixture fixture,
        long number,
        OrganizationAccessContext? actor = null
    )
    {
        actor ??= fixture.Approver;
        return fixture.RunAsync(
            actor,
            provider =>
                provider
                    .GetRequiredService<ISalesOrderOperations>()
                    .GetFulfilmentAsync(
                        actor.UserId,
                        actor.OrganizationId,
                        number,
                        TestContext.Current.CancellationToken
                    )
        );
    }

    private static Task<object> RevokeAsync(SalesApprovalFixture fixture, string kind) =>
        fixture.RunAsync<object>(
            fixture.Administrator,
            async provider =>
                kind switch
                {
                    "authority" => await provider
                        .GetRequiredService<ISalesApprovalAuthorityAdministration>()
                        .RevokeAsync(
                            new(
                                fixture.Administrator.UserId,
                                fixture.Administrator.OrganizationId,
                                fixture.Approver.MembershipId,
                                1
                            ),
                            TestContext.Current.CancellationToken
                        ),
                    "role" => await provider
                        .GetRequiredService<IOrganizationMembershipAdministration>()
                        .ReplaceRolesAsync(
                            new(
                                fixture.Administrator.UserId,
                                fixture.Administrator.OrganizationId,
                                fixture.Approver.MembershipId,
                                [SalesRoleIds.Clerk]
                            ),
                            TestContext.Current.CancellationToken
                        ),
                    "membership" => await provider
                        .GetRequiredService<IOrganizationMembershipAdministration>()
                        .ChangeStatusAsync(
                            new(
                                fixture.Administrator.UserId,
                                fixture.Administrator.OrganizationId,
                                fixture.Approver.MembershipId,
                                MembershipStatus.Suspended
                            ),
                            TestContext.Current.CancellationToken
                        ),
                    _ => throw new ArgumentOutOfRangeException(nameof(kind)),
                }
        );

    private static void AssertSuccessfulRevocation(string kind, object result)
    {
        switch (kind)
        {
            case "authority":
                Assert.IsType<RevokeSalesApprovalAuthorityResult.Revoked>(result);
                break;
            case "role":
                Assert.IsType<ReplaceMembershipRolesResult.Updated>(result);
                break;
            case "membership":
                Assert.IsType<ChangeMembershipStatusResult.Changed>(result);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task InstallApprovalPauseAsync(SalesApprovalFixture fixture)
    {
        await using NpgsqlConnection connection = await fixture.OpenConnectionAsync();
        await ExecuteAsync(
            connection,
            """
            CREATE FUNCTION sales.pause_approval() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.action = 'sales-order.approved' THEN PERFORM pg_advisory_xact_lock(704201); END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER pause_approval BEFORE INSERT ON sales.audit_entries
            FOR EACH ROW EXECUTE FUNCTION sales.pause_approval();
            """
        );
    }

    private static async Task WaitForLockAsync(
        SalesApprovalFixture fixture,
        string kind,
        Task operation
    )
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken
        );
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        await using NpgsqlConnection connection = await fixture.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            kind == "advisory"
                ? "SELECT EXISTS (SELECT 1 FROM pg_locks WHERE locktype = 'advisory' AND objid = 704201 AND NOT granted)"
                : "SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE wait_event_type = 'Lock' AND wait_event <> 'advisory' AND (query ILIKE '%approval_authorities%' OR query ILIKE '%access.organizations%'))",
            connection
        );
        while (!(bool)(await command.ExecuteScalarAsync(timeout.Token))!)
        {
            if (operation.IsCompleted)
                return;
            await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);
        }
    }
}
