using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Purchasing.Contracts;
using ModulithFoundry.Modules.Sales.Contracts;

namespace ModulithFoundry.BrokerTests;

public sealed class InventoryDeliveryRecoveryTests
{
    [Fact]
    public async Task RequeueOutcome_InventoryRoleRevoked_RejectsPreviouslyAuthorizedContext()
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync();
        var (_, original) = await PrepareFailedReleaseAsync(fixture);
        string[] remainingRoles =
        [
            SystemRoleIds.OrganizationAdministrator,
            SalesRoleIds.Manager,
            SalesRoleIds.Clerk,
            PurchasingRoleIds.Agent,
        ];
        await fixture.RunAsync(
            fixture.Administrator,
            async services =>
            {
                Assert.IsType<ReplaceMembershipRolesResult.Updated>(
                    await services
                        .GetRequiredService<IOrganizationMembershipAdministration>()
                        .ReplaceRolesAsync(
                            new(
                                fixture.Administrator.UserId,
                                fixture.Administrator.OrganizationId,
                                fixture.Administrator.MembershipId,
                                remainingRoles
                            ),
                            fixture.CancellationToken
                        )
                );
            }
        );
        Assert.IsType<RequeueInventoryMessageResult.PermissionDenied>(
            await RequeueAsync(fixture, original)
        );
        Assert.IsType<GetInventoryMessageDeliveryResult.PermissionDenied>(
            await fixture.RunAsync(
                fixture.Administrator,
                services =>
                    services
                        .GetRequiredService<IInventoryMessageDeliveryRecovery>()
                        .GetAsync(
                            new(
                                fixture.Administrator.UserId,
                                fixture.Administrator.OrganizationId,
                                original.MessageId
                            ),
                            fixture.CancellationToken
                        )
            )
        );
        await fixture.RunAsync(
            fixture.Administrator,
            async services =>
            {
                Assert.IsType<ReplaceMembershipRolesResult.Updated>(
                    await services
                        .GetRequiredService<IOrganizationMembershipAdministration>()
                        .ReplaceRolesAsync(
                            new(
                                fixture.Administrator.UserId,
                                fixture.Administrator.OrganizationId,
                                fixture.Administrator.MembershipId,
                                [.. remainingRoles, InventoryRoleIds.Manager]
                            ),
                            fixture.CancellationToken
                        )
                );
            }
        );
        Assert.Equal(original, await InspectAsync(fixture, original.MessageId));
        Assert.Equal(4, (await fixture.StockAsync()).Version);
    }

    [Fact]
    public async Task RequeueOutcome_UnsupportedRetainedType_RefusesArbitraryReplay()
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync();
        var (_, original) = await PrepareFailedReleaseAsync(fixture);
        await fixture.StopAsync();
        await fixture.ExecuteSetupAsync(
            $"""
            UPDATE inventory.outbox_messages SET message_type = '{StockItemReferenceChangedV1.LogicalName}'
            WHERE message_id = '{original.MessageId}';
            """
        );
        var before = await InspectAsync(fixture, original.MessageId);
        Assert.IsType<RequeueInventoryMessageResult.UnsupportedMessageType>(
            await RequeueAsync(fixture, before)
        );
        Assert.Equal(before, await InspectAsync(fixture, original.MessageId));
        Assert.Equal(4, (await fixture.StockAsync()).Version);
    }

    [Fact]
    public async Task RequeueOutcome_CompetingOperators_QueuesOneRecoveryGeneration()
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync();
        var (_, original) = await PrepareFailedReleaseAsync(fixture);
        await fixture.StopAsync();
        var results = await Task.WhenAll(
            RequeueAsync(fixture, original),
            RequeueAsync(fixture, original)
        );
        var queued = Assert
            .Single(results.OfType<RequeueInventoryMessageResult.Requeued>())
            .Delivery;
        Assert.Single(results.OfType<RequeueInventoryMessageResult.AlreadyQueued>());
        Assert.Equal(queued, await InspectAsync(fixture, original.MessageId));
        Assert.Equal(original.MessageId, queued.MessageId);
        Assert.Equal(original.DispatchAttempts, queued.DispatchAttempts);
        Assert.Equal(4, (await fixture.StockAsync()).Version);
    }

    [Theory]
    [InlineData("audit_entries")]
    [InlineData("outbox_messages")]
    public async Task RequeueOutcome_AtomicParticipantFails_RollsBackAndRetriesWithOneRecoveryAudit(
        string table
    )
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync();
        var (_, original) = await PrepareFailedReleaseAsync(fixture);
        await fixture.StopAsync();
        string guard =
            table == "audit_entries"
                ? "IF NEW.action = 'message-delivery.requeued' THEN"
                : "IF TRUE THEN";
        string operation = table == "audit_entries" ? "INSERT" : "UPDATE";
        await fixture.ExecuteSetupAsync(
            $"""
            CREATE UNIQUE INDEX test_recovery_audit_once ON inventory.audit_entries(subject_id)
                WHERE action = 'message-delivery.requeued';
            CREATE FUNCTION inventory.reject_test_recovery() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                {guard} RAISE EXCEPTION 'test recovery participant failure'; END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER reject_test_recovery BEFORE {operation} ON inventory.{table}
            FOR EACH ROW EXECUTE FUNCTION inventory.reject_test_recovery();
            """
        );
        await Assert.ThrowsAsync<DbUpdateException>(() => RequeueAsync(fixture, original));
        Assert.Equal(original, await InspectAsync(fixture, original.MessageId));
        await fixture.ExecuteSetupAsync(
            $"""
            DROP TRIGGER reject_test_recovery ON inventory.{table};
            DROP FUNCTION inventory.reject_test_recovery();
            """
        );
        var queued = Assert
            .IsType<RequeueInventoryMessageResult.Requeued>(await RequeueAsync(fixture, original))
            .Delivery;
        // The test-only uniqueness constraint would reject this retry if the failed
        // transaction retained an audit; a repeated pending request must not audit again.
        Assert.IsType<RequeueInventoryMessageResult.AlreadyQueued>(
            await RequeueAsync(fixture, queued)
        );
        Assert.Equal(queued, await InspectAsync(fixture, original.MessageId));
        Assert.Equal(4, (await fixture.StockAsync()).Version);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RequeueOutcome_RetainedLease_RefusesActiveAndRecoversExpired(bool expired)
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync();
        var (_, original) = await PrepareFailedReleaseAsync(fixture);
        await fixture.StopAsync();
        string leaseOffset = expired ? "- interval '1 second'" : "+ interval '2 minutes'";
        await fixture.ExecuteSetupAsync(
            $"""
            UPDATE inventory.outbox_messages SET dispatched_at = NULL,
                available_at = clock_timestamp() + interval '5 minutes',
                lease_token = '{Guid.CreateVersion7()}', lease_until = clock_timestamp() {leaseOffset}
            WHERE message_id = '{original.MessageId}';
            """
        );
        var before = await InspectAsync(fixture, original.MessageId);
        var result = await RequeueAsync(fixture, before);
        if (expired)
        {
            var queued = Assert.IsType<RequeueInventoryMessageResult.Requeued>(result).Delivery;
            Assert.Null(queued.LeaseExpiresAt);
            Assert.Null(queued.LastPublishedAt);
            Assert.Equal(original.DispatchAttempts, queued.DispatchAttempts);
            Assert.IsType<RequeueInventoryMessageResult.AlreadyQueued>(
                await RequeueAsync(fixture, queued)
            );
            Assert.Equal(queued, await InspectAsync(fixture, original.MessageId));
        }
        else
        {
            Assert.IsType<RequeueInventoryMessageResult.InFlight>(result);
            Assert.Equal(before, await InspectAsync(fixture, original.MessageId));
        }
        Assert.Equal(4, (await fixture.StockAsync()).Version);
    }

    [Fact]
    public async Task RequeueOutcome_InvalidActorScopeOrInput_DoesNotChangeRetainedDelivery()
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync();
        var (_, original) = await PrepareFailedReleaseAsync(fixture);
        var command = new RequeueInventoryMessageCommand(
            fixture.Administrator.UserId,
            fixture.Administrator.OrganizationId,
            original.MessageId,
            original.DispatchAttempts,
            "Receiver repaired"
        );
        Assert.IsType<GetInventoryMessageDeliveryResult.PermissionDenied>(
            await fixture.RunAsync(
                fixture.Approver,
                services =>
                    services
                        .GetRequiredService<IInventoryMessageDeliveryRecovery>()
                        .GetAsync(
                            new(
                                fixture.Approver.UserId,
                                fixture.Approver.OrganizationId,
                                original.MessageId
                            ),
                            fixture.CancellationToken
                        )
            )
        );
        Assert.IsType<RequeueInventoryMessageResult.PermissionDenied>(
            await fixture.RunAsync(
                fixture.Approver,
                services =>
                    services
                        .GetRequiredService<IInventoryMessageDeliveryRecovery>()
                        .RequeueAsync(
                            command with
                            {
                                ActorUserId = fixture.Approver.UserId,
                            },
                            fixture.CancellationToken
                        )
            )
        );
        foreach (
            var forged in new[]
            {
                command with
                {
                    ActorUserId = new UserId(Guid.CreateVersion7()),
                },
                command with
                {
                    OrganizationId = new OrganizationId(Guid.CreateVersion7()),
                },
            }
        )
            Assert.IsType<RequeueInventoryMessageResult.PermissionDenied>(
                await fixture.RunAsync(
                    fixture.Administrator,
                    services =>
                        services
                            .GetRequiredService<IInventoryMessageDeliveryRecovery>()
                            .RequeueAsync(forged, fixture.CancellationToken)
                )
            );
        Assert.IsType<RequeueInventoryMessageResult.PermissionDenied>(
            await fixture.RunAsync(
                null,
                services =>
                    services
                        .GetRequiredService<IInventoryMessageDeliveryRecovery>()
                        .RequeueAsync(command, fixture.CancellationToken)
            )
        );
        await fixture.RunAsync(
            fixture.Administrator,
            async services =>
            {
                var recovery = services.GetRequiredService<IInventoryMessageDeliveryRecovery>();
                Assert.IsType<RequeueInventoryMessageResult.InvalidExpectedDispatchAttempts>(
                    await recovery.RequeueAsync(
                        command with
                        {
                            ExpectedDispatchAttempts = -1,
                        },
                        fixture.CancellationToken
                    )
                );
                Assert.IsType<RequeueInventoryMessageResult.InvalidReason>(
                    await recovery.RequeueAsync(
                        command with
                        {
                            Reason = " ",
                        },
                        fixture.CancellationToken
                    )
                );
                Assert.IsType<RequeueInventoryMessageResult.NotFound>(
                    await recovery.RequeueAsync(
                        command with
                        {
                            MessageId = Guid.CreateVersion7(),
                        },
                        fixture.CancellationToken
                    )
                );
                Assert.IsType<GetInventoryMessageDeliveryResult.NotFound>(
                    await recovery.GetAsync(
                        new(command.ActorUserId, command.OrganizationId, Guid.CreateVersion7()),
                        fixture.CancellationToken
                    )
                );
            }
        );
        Assert.Equal(original, await WaitPublishedAsync(fixture, original.MessageId));
        Assert.Equal(4, (await fixture.StockAsync()).Version);
    }

    [Fact]
    public async Task RequeueOutcome_ReusedScopeAfterRelayAdvances_RejectsStaleOperatorSnapshot()
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync();
        var (_, original) = await PrepareFailedReleaseAsync(fixture);
        await RestoreReceiverAsync(fixture);
        await fixture.RunAsync(
            fixture.Administrator,
            async services =>
            {
                var recovery = services.GetRequiredService<IInventoryMessageDeliveryRecovery>();
                var command = new RequeueInventoryMessageCommand(
                    fixture.Administrator.UserId,
                    fixture.Administrator.OrganizationId,
                    original.MessageId,
                    original.DispatchAttempts,
                    "Receiver repaired"
                );
                Assert.IsType<RequeueInventoryMessageResult.Requeued>(
                    await recovery.RequeueAsync(command, fixture.CancellationToken)
                );
                await WaitPublishedAsync(fixture, original.MessageId, original.DispatchAttempts);
                Assert.IsType<RequeueInventoryMessageResult.VersionConflict>(
                    await recovery.RequeueAsync(command, fixture.CancellationToken)
                );
            }
        );
        Assert.Equal(4, (await fixture.StockAsync()).Version);
    }

    [Fact]
    public async Task RequeueOutcome_SalesRejectedDelivery_RepublishesOriginalWithoutAnotherStockEffect()
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync();
        var (order, original) = await PrepareFailedReleaseAsync(fixture);
        Assert.Equal(0m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(
            OrderFulfilmentStatus.CompensationPending,
            (await fixture.ReadAsync(order.OrderNumber)).Status
        );
        await RestoreReceiverAsync(fixture);
        Assert.IsType<RequeueInventoryMessageResult.Requeued>(
            await RequeueAsync(fixture, original)
        );
        var completed = await fixture.WaitAsync(order.OrderNumber, "compensated");
        await fixture.WaitDeliveryAsync(original.MessageId);
        var published = await WaitPublishedAsync(
            fixture,
            original.MessageId,
            original.DispatchAttempts
        );
        Assert.Equal(original.MessageId, published.MessageId);
        Assert.Equal(original.MessageType, published.MessageType);
        Assert.Equal(original.CreatedAt, published.CreatedAt);
        Assert.Equal(original.DispatchAttempts + 1, published.DispatchAttempts);
        Assert.Equal(0m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(4, (await fixture.StockAsync()).Version);
        Assert.Single(
            await fixture.ActivityAsync(order.OrderNumber),
            item => item.Kind == SalesOrderActivityKind.CompensationCompleted
        );
        var history = await fixture.RunAsync(
            fixture.Administrator,
            async services =>
                Assert
                    .IsType<GetStockPositionHistoryResult.Found>(
                        await services
                            .GetRequiredService<IStockPositionOperations>()
                            .GetHistoryAsync(
                                new(
                                    fixture.Administrator.UserId,
                                    fixture.Administrator.OrganizationId,
                                    "MAIN",
                                    "BOLT"
                                ),
                                fixture.CancellationToken
                            )
                    )
                    .History
        );
        Assert.Single(
            history.Entries,
            entry => entry.Action == StockPositionHistoryAction.ReservationReleased
        );
        Assert.Equal(OrderFulfilmentStatus.Compensated, completed.Status);
    }

    private static async Task<InventoryMessageDeliveryView> WaitPublishedAsync(
        SalesFulfilmentFixture fixture,
        Guid messageId,
        int afterAttempts = -1
    )
    {
        while (true)
        {
            var view = await InspectAsync(fixture, messageId);
            if (view.LastPublishedAt.HasValue && view.DispatchAttempts > afterAttempts)
                return view;
            await Task.Delay(50, fixture.CancellationToken);
        }
    }

    private static async Task<InventoryMessageDeliveryView> InspectAsync(
        SalesFulfilmentFixture fixture,
        Guid messageId
    ) =>
        Assert
            .IsType<GetInventoryMessageDeliveryResult.Found>(
                await fixture.RunAsync(
                    fixture.Administrator,
                    services =>
                        services
                            .GetRequiredService<IInventoryMessageDeliveryRecovery>()
                            .GetAsync(
                                new(
                                    fixture.Administrator.UserId,
                                    fixture.Administrator.OrganizationId,
                                    messageId
                                ),
                                fixture.CancellationToken
                            )
                )
            )
            .Delivery;

    private static async Task<(
        SalesOrderView Order,
        InventoryMessageDeliveryView Delivery
    )> PrepareFailedReleaseAsync(SalesFulfilmentFixture fixture)
    {
        var order = await fixture.ApproveAsync(4m);
        await fixture.WaitAsync(order.OrderNumber, "reserved");
        await fixture.ExecuteSetupAsync(
            """
            CREATE FUNCTION sales.reject_test_release() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.action = 'order-fulfilment.release-outcome-recorded' THEN
                    RAISE EXCEPTION 'test lost release response';
                END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER reject_test_release BEFORE INSERT ON sales.audit_entries
            FOR EACH ROW EXECUTE FUNCTION sales.reject_test_release();
            """
        );
        Assert.IsType<CancelSalesOrderResult.Cancelled>(await fixture.CancelAsync(order));
        var outcome = await fixture.ReadReleaseErrorAsync();
        return (order, await WaitPublishedAsync(fixture, outcome.MessageId));
    }

    private static Task RestoreReceiverAsync(SalesFulfilmentFixture fixture) =>
        fixture.ExecuteSetupAsync(
            """
            DROP TRIGGER reject_test_release ON sales.audit_entries;
            DROP FUNCTION sales.reject_test_release();
            """
        );

    private static Task<RequeueInventoryMessageResult> RequeueAsync(
        SalesFulfilmentFixture fixture,
        InventoryMessageDeliveryView delivery
    ) =>
        fixture.RunAsync(
            fixture.Administrator,
            services =>
                services
                    .GetRequiredService<IInventoryMessageDeliveryRecovery>()
                    .RequeueAsync(
                        new(
                            fixture.Administrator.UserId,
                            fixture.Administrator.OrganizationId,
                            delivery.MessageId,
                            delivery.DispatchAttempts,
                            "Receiver fault repaired; retry retained outcome"
                        ),
                        fixture.CancellationToken
                    )
        );
}
