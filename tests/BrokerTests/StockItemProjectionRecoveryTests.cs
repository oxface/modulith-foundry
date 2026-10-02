using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Purchasing.Contracts;

namespace ModulithFoundry.BrokerTests;

public sealed class StockItemProjectionRecoveryTests
{
    [Fact]
    public async Task Reconciliation_DamagedAndMissingRows_ReportsThenRepairsAuthoritativeState()
    {
        await using StockItemBootstrapFixture fixture =
            await StockItemBootstrapFixture.StartAsync();
        StockItemId bolt = await fixture.CreateAsync("BOLT");
        StockItemId nut = await fixture.CreateAsync("NUT");
        await fixture.PublishHistoryBeforePurchasingSubscribesAsync();
        await fixture.BootstrapAsync();
        await fixture.ExecuteSqlAsync(
            $"""
            UPDATE purchasing.stock_item_references SET description = 'Damaged', sku = 'BAD', base_unit_code = 'BAD'
            WHERE stock_item_id = '{bolt.Value}';
            DELETE FROM purchasing.stock_item_references WHERE stock_item_id = '{nut.Value}';
            """
        );

        StockItemProjectionComparison comparison = await fixture.CompareAsync();
        Assert.False(comparison.IsConsistent);
        Assert.Equal(1, comparison.Missing);
        Assert.Equal(1, comparison.Different);
        Assert.Equal("Damaged", (await fixture.GetAsync(bolt))!.Description);
        Assert.Null(await fixture.GetAsync(nut));
        await fixture.CompareAsync(repair: true);
        Assert.True((await fixture.CompareAsync()).IsConsistent);
        Assert.Equal("Bolt", (await fixture.GetAsync(bolt))!.Description);
        Assert.Equal("BOLT", (await fixture.GetAsync(bolt))!.Sku);
        Assert.Equal("EA", (await fixture.GetAsync(bolt))!.BaseUnitCode);
        Assert.NotNull(await fixture.GetAsync(nut));
        Assert.Equal(2, (await fixture.StatusAsync()).SnapshotWatermark);
    }

    [Fact]
    public async Task Reconciliation_UpdateArrivesDuringSnapshot_PreservesNewerTail()
    {
        await using StockItemBootstrapFixture fixture =
            await StockItemBootstrapFixture.StartAsync();
        StockItemId id = await fixture.CreateAsync("BOLT");
        await fixture.BootstrapAsync();
        fixture.PauseSnapshotAfterWatermark();
        Task<StockItemProjectionComparison> repairing = Task.Run(() =>
            fixture.CompareAsync(repair: true)
        );
        await fixture.WaitForSnapshotWatermarkAsync();
        try
        {
            await fixture.ChangeDescriptionAsync("BOLT", "Newer than repair snapshot");
            await fixture.WaitForDescriptionAsync(id, "Newer than repair snapshot");
        }
        finally
        {
            fixture.ReleaseSnapshot();
        }
        Assert.Equal(1, (await repairing).PreservedNewer);
        Assert.Equal("Newer than repair snapshot", (await fixture.GetAsync(id))!.Description);
        Assert.Equal(2, (await fixture.GetAsync(id))!.SourceRevision);
        Assert.Equal(1, (await fixture.StatusAsync()).SnapshotWatermark);
    }

    [Fact]
    public async Task Consumer_SeparateWorkerTakesStableQueue_ContinuesTailWithoutRebootstrap()
    {
        await using StockItemBootstrapFixture fixture =
            await StockItemBootstrapFixture.StartAsync();
        StockItemId id = await fixture.CreateAsync("BOLT");
        await fixture.BootstrapAsync();
        await fixture.StartProducerOnlyAsync(); // Stop the in-process receiver before the worker owns its queue.
        await fixture.ChangeDescriptionAsync("BOLT", "Queued during cutover");
        await using ReceiverProcess worker = await ReceiverProcess.StartPurchasingAsync(fixture);
        await fixture.WaitForDescriptionAsync(id, "Queued during cutover");
        await fixture.ChangeDescriptionAsync("BOLT", "Worker update");
        await fixture.WaitForDescriptionAsync(id, "Worker update");
        Assert.Equal(3, (await fixture.GetAsync(id))!.SourceRevision);
        Assert.Equal(1, (await fixture.StatusAsync()).SnapshotWatermark);
    }

    [Fact]
    public async Task Reconciliation_CheckpointSaveFails_RollsBackRepairAndCanRetry()
    {
        await using StockItemBootstrapFixture fixture =
            await StockItemBootstrapFixture.StartAsync();
        StockItemId id = await fixture.CreateAsync("BOLT");
        await fixture.BootstrapAsync();
        await fixture.ChangeDescriptionAsync("BOLT", "Source update");
        await fixture.WaitForDescriptionAsync(id, "Source update");
        await fixture.ExecuteSqlAsync(
            """
            UPDATE purchasing.stock_item_references SET description = 'Damaged';
            CREATE FUNCTION purchasing.fail_repair() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'test checkpoint failure'; END; $$;
            CREATE TRIGGER fail_repair BEFORE UPDATE ON purchasing.stock_item_bootstrap
            FOR EACH ROW EXECUTE FUNCTION purchasing.fail_repair();
            """
        );
        await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() =>
            fixture.CompareAsync(repair: true)
        );
        Assert.Equal("Damaged", (await fixture.GetAsync(id))!.Description);
        Assert.Equal(1, (await fixture.StatusAsync()).SnapshotWatermark);
        await fixture.ExecuteSqlAsync(
            "DROP TRIGGER fail_repair ON purchasing.stock_item_bootstrap; DROP FUNCTION purchasing.fail_repair();"
        );
        await fixture.CompareAsync(repair: true);
        Assert.Equal("Source update", (await fixture.GetAsync(id))!.Description);
        Assert.Equal(2, (await fixture.StatusAsync()).SnapshotWatermark);
    }

    [Fact]
    public async Task Bootstrap_AbruptExitDuringSnapshot_RetriesAgainstRetainedBufferedTail()
    {
        await using StockItemBootstrapFixture fixture =
            await StockItemBootstrapFixture.StartAsync();
        StockItemId id = await fixture.CreateAsync("BOLT");
        await fixture.StartProducerOnlyAsync();
        await using (
            ReceiverProcess child = await ReceiverProcess.StartPurchasingAsync(
                fixture,
                pauseSnapshot: true
            )
        )
        {
            await child.WaitForSignalAsync("snapshot", fixture.CancellationToken);
            await fixture.ChangeDescriptionAsync("BOLT", "Buffered before crash");
            StockItemReferenceStateV1 latest = Assert.Single((await fixture.ExportAsync()).Items);
            var buffered = new StockItemReferenceChangedV1(
                Guid.CreateVersion7(),
                latest,
                DateTimeOffset.UtcNow
            );
            await fixture.SendAsync(buffered);
            await child.WaitForSignalAsync(
                $"handled:{buffered.MessageId}",
                fixture.CancellationToken
            );
            Assert.False((await fixture.StatusAsync()).IsReady);
            Assert.Null(await fixture.GetAsync(id));
            await child.KillAsync();
        }
        await using ReceiverProcess restarted = await ReceiverProcess.StartPurchasingAsync(fixture);
        await fixture.WaitForReadyAsync();
        Assert.Equal("Buffered before crash", (await fixture.GetAsync(id))!.Description);
        Assert.Equal(2, (await fixture.GetAsync(id))!.SourceRevision);
        Assert.Equal(2, (await fixture.StatusAsync()).SnapshotWatermark);
    }

    [Fact]
    public async Task Bootstrap_AbruptExitDuringInstallation_RollsBackReadinessAndRetries()
    {
        await using StockItemBootstrapFixture fixture =
            await StockItemBootstrapFixture.StartAsync();
        StockItemId id = await fixture.CreateAsync("BOLT");
        await fixture.PublishHistoryBeforePurchasingSubscribesAsync();
        await fixture.StartProducerOnlyAsync();
        await using (
            ReceiverDatabaseBarrier barrier = await ReceiverDatabaseBarrier.CreatePurchasingAsync(
                fixture,
                installation: true
            )
        )
        {
            await using ReceiverProcess child = await ReceiverProcess.StartPurchasingAsync(fixture);
            await barrier.WaitAsync(child);
            Assert.False((await fixture.StatusAsync()).IsReady);
            Assert.Null((await fixture.StatusAsync()).SnapshotWatermark);
            Assert.Null(await fixture.GetAsync(id));
            await child.KillAsync();
            await barrier.WaitForDisconnectAsync(child);
        }
        await using ReceiverProcess restarted = await ReceiverProcess.StartPurchasingAsync(fixture);
        await fixture.WaitForReadyAsync();
        Assert.Equal("Bolt", (await fixture.GetAsync(id))!.Description);
        Assert.Equal(1, (await fixture.StatusAsync()).SnapshotWatermark);
    }

    [Fact]
    public async Task Bootstrap_AbruptExitAfterInstallation_RetainsBoundaryAndResumesQueuedTail()
    {
        await using StockItemBootstrapFixture fixture =
            await StockItemBootstrapFixture.StartAsync();
        StockItemId id = await fixture.CreateAsync("BOLT");
        await fixture.PublishHistoryBeforePurchasingSubscribesAsync();
        await fixture.StartProducerOnlyAsync();
        await using (ReceiverProcess child = await ReceiverProcess.StartPurchasingAsync(fixture))
        {
            await fixture.WaitForReadyAsync();
            Assert.Equal("Bolt", (await fixture.GetAsync(id))!.Description);
            await child.KillAsync();
        }
        await fixture.ChangeDescriptionAsync("BOLT", "After committed bootstrap");
        // This checkpoint would block any new export: a Ready consumer must only resume its tail.
        await using ReceiverProcess restarted = await ReceiverProcess.StartPurchasingAsync(
            fixture,
            pauseSnapshot: true
        );
        await fixture.WaitForDescriptionAsync(id, "After committed bootstrap");
        Assert.Equal(2, (await fixture.GetAsync(id))!.SourceRevision);
        Assert.Equal(1, (await fixture.StatusAsync()).SnapshotWatermark);
    }

    [Fact]
    public async Task Tail_AbruptExitBeforeCommit_RollsBackReceiptAndRedeliversUpdate()
    {
        await using StockItemBootstrapFixture fixture =
            await StockItemBootstrapFixture.StartAsync();
        StockItemId id = await fixture.CreateAsync("BOLT");
        await fixture.PublishHistoryBeforePurchasingSubscribesAsync();
        await fixture.BootstrapAsync();
        await fixture.StartProducerOnlyAsync();
        StockItemReferenceChangedV1 update = Update(fixture, id);
        await using (
            ReceiverDatabaseBarrier barrier = await ReceiverDatabaseBarrier.CreatePurchasingAsync(
                fixture,
                installation: false
            )
        )
        {
            await using ReceiverProcess child = await ReceiverProcess.StartPurchasingAsync(fixture);
            await fixture.SendAsync(update);
            await barrier.WaitAsync(child);
            Assert.Equal("Bolt", (await fixture.GetAsync(id))!.Description);
            Assert.Equal(1, (await fixture.GetAsync(id))!.SourceRevision);
            await child.KillAsync();
            await barrier.WaitForDisconnectAsync(child);
        }
        await using ReceiverProcess restarted = await ReceiverProcess.StartPurchasingAsync(fixture);
        await restarted.WaitForSignalAsync(
            $"handled:{update.MessageId}",
            fixture.CancellationToken
        );
        Assert.Equal("Updated", (await fixture.GetAsync(id))!.Description);
        Assert.Equal(2, (await fixture.GetAsync(id))!.SourceRevision);
    }

    [Fact]
    public async Task Tail_AbruptExitAfterCommitBeforeAck_RedeliversWithoutRepeatedEffect()
    {
        await using StockItemBootstrapFixture fixture =
            await StockItemBootstrapFixture.StartAsync();
        StockItemId id = await fixture.CreateAsync("BOLT");
        await fixture.PublishHistoryBeforePurchasingSubscribesAsync();
        await fixture.BootstrapAsync();
        await fixture.StartProducerOnlyAsync();
        StockItemReferenceChangedV1 update = Update(fixture, id);
        await using (
            ReceiverProcess child = await ReceiverProcess.StartPurchasingAsync(
                fixture,
                pauseAfterCommit: true
            )
        )
        {
            await fixture.SendAsync(update);
            await child.WaitForSignalAsync(
                $"committed:{update.MessageId}",
                fixture.CancellationToken
            );
            Assert.Equal("Updated", (await fixture.GetAsync(id))!.Description);
            await child.KillAsync();
        }
        await using ReceiverProcess restarted = await ReceiverProcess.StartPurchasingAsync(fixture);
        await restarted.WaitForSignalAsync(
            $"handled:{update.MessageId}",
            fixture.CancellationToken
        );
        Assert.Equal("Updated", (await fixture.GetAsync(id))!.Description);
        Assert.Equal(2, (await fixture.GetAsync(id))!.SourceRevision);
        Assert.Equal(1, (await fixture.StatusAsync()).SnapshotWatermark);
    }

    private static StockItemReferenceChangedV1 Update(
        StockItemBootstrapFixture fixture,
        StockItemId id
    ) =>
        new(
            Guid.CreateVersion7(),
            new(fixture.Actor.OrganizationId.Value, id.Value, "BOLT", "Updated", "EA", true, 2),
            DateTimeOffset.UtcNow
        );

    [Fact]
    public async Task Tail_StorageFailure_RoutesToPurchasingErrorQueueAndExplicitRedriveSucceeds()
    {
        await using StockItemBootstrapFixture fixture =
            await StockItemBootstrapFixture.StartAsync();
        StockItemId id = await fixture.CreateAsync("BOLT");
        await fixture.PublishHistoryBeforePurchasingSubscribesAsync();
        await fixture.BootstrapAsync();
        await fixture.StartErrorObserverAsync();
        await fixture.ExecuteSqlAsync(
            """
            CREATE FUNCTION purchasing.fail_receipt() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'test receipt failure'; END; $$;
            CREATE TRIGGER fail_receipt BEFORE INSERT ON purchasing.stock_item_reference_inbox
            FOR EACH ROW EXECUTE FUNCTION purchasing.fail_receipt();
            """
        );
        StockItemReferenceChangedV1 update = Update(fixture, id);
        await fixture.SendAsync(update);
        StockItemReferenceChangedV1 failed = await fixture.ReadErrorAsync();
        Assert.Equal(update, failed);
        Assert.Equal("Bolt", (await fixture.GetAsync(id))!.Description);
        await fixture.ExecuteSqlAsync(
            "DROP TRIGGER fail_receipt ON purchasing.stock_item_reference_inbox; DROP FUNCTION purchasing.fail_receipt();"
        );
        // Deliberate test/operator action preserving the original identity and content.
        await fixture.SendAsync(failed);
        await fixture.WaitForProcessedAsync(update.MessageId);
        await fixture.SendAsync(failed);
        await fixture.WaitForProcessedAsync(update.MessageId, 2);
        Assert.Equal("Updated", (await fixture.GetAsync(id))!.Description);
        Assert.Equal(2, (await fixture.GetAsync(id))!.SourceRevision);
    }

    [Fact]
    public async Task Tail_ConflictingDeliveryOrRevision_IsolatesPoisonWithoutOverwritingState()
    {
        await using StockItemBootstrapFixture fixture =
            await StockItemBootstrapFixture.StartAsync();
        StockItemId id = await fixture.CreateAsync("BOLT");
        await fixture.PublishHistoryBeforePurchasingSubscribesAsync();
        await fixture.BootstrapAsync();
        await fixture.StartErrorObserverAsync();
        StockItemReferenceChangedV1 accepted = Update(fixture, id);
        await fixture.SendAsync(accepted);
        await fixture.WaitForProcessedAsync(accepted.MessageId);
        StockItemReferenceChangedV1 conflict = accepted with
        {
            Item = accepted.Item with { Description = "Conflicting" },
        };
        await fixture.SendAsync(conflict);
        Assert.Equal(conflict, await fixture.ReadErrorAsync());
        StockItemReferenceChangedV1 sameRevision = conflict with
        {
            MessageId = Guid.CreateVersion7(),
        };
        await fixture.SendAsync(sameRevision);
        Assert.Equal(sameRevision, await fixture.ReadErrorAsync());
        Assert.Equal("Updated", (await fixture.GetAsync(id))!.Description);
        StockItemReferenceChangedV1 corrected = sameRevision with
        {
            MessageId = Guid.CreateVersion7(),
            Item = accepted.Item,
        };
        await fixture.SendAsync(corrected);
        await fixture.WaitForProcessedAsync(corrected.MessageId);
        Assert.Equal(2, (await fixture.GetAsync(id))!.SourceRevision);
    }

    [Fact]
    public async Task Reconciliation_UnexpectedCoveredRow_RemovesOnlyConsumerProjectionDrift()
    {
        await using StockItemBootstrapFixture fixture =
            await StockItemBootstrapFixture.StartAsync();
        StockItemId id = await fixture.CreateAsync("BOLT");
        await fixture.BootstrapAsync();
        Guid unexpectedId = Guid.CreateVersion7();
        await fixture.ExecuteSqlAsync(
            $"""
            INSERT INTO purchasing.stock_item_references
            (organization_id, stock_item_id, sku, description, base_unit_code, is_active, source_revision)
            VALUES ('{fixture.Actor.OrganizationId.Value}', '{unexpectedId}', 'GHOST', 'Unexpected', 'EA', true, 1);
            """
        );
        Assert.Equal(1, (await fixture.CompareAsync()).Unexpected);
        await fixture.CompareAsync(repair: true);
        Assert.Null(await fixture.GetAsync(fixture.Actor.OrganizationId, unexpectedId));
        Assert.NotNull(await fixture.GetAsync(id));
        Assert.Single((await fixture.ExportAsync()).Items);
    }

    [Fact]
    public async Task Reconciliation_SourceWatermarkRegresses_RejectsUnsafeIndependentRestore()
    {
        await using StockItemBootstrapFixture fixture =
            await StockItemBootstrapFixture.StartAsync();
        StockItemId id = await fixture.CreateAsync("BOLT");
        await fixture.BootstrapAsync();
        await fixture.ExecuteSqlAsync(
            "UPDATE inventory.stock_item_reference_feed SET revision = 0;"
        );
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.CompareAsync(repair: true));
        Assert.Equal(1, (await fixture.StatusAsync()).SnapshotWatermark);
        Assert.Equal("Bolt", (await fixture.GetAsync(id))!.Description);
    }

    [Fact]
    public async Task Reconciliation_ErrorQueuedRealUpdate_RepairsMissedTailAndMakesLateRedriveHarmless()
    {
        await using StockItemBootstrapFixture fixture =
            await StockItemBootstrapFixture.StartAsync();
        StockItemId id = await fixture.CreateAsync("BOLT");
        await fixture.PublishHistoryBeforePurchasingSubscribesAsync();
        await fixture.BootstrapAsync();
        await fixture.StartErrorObserverAsync();
        await fixture.ExecuteSqlAsync(
            """
            CREATE FUNCTION purchasing.fail_reference() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'test projection failure'; END; $$;
            CREATE TRIGGER fail_reference BEFORE UPDATE ON purchasing.stock_item_references
            FOR EACH ROW EXECUTE FUNCTION purchasing.fail_reference();
            """
        );
        await fixture.ChangeDescriptionAsync("BOLT", "Authoritative repair");
        StockItemReferenceChangedV1 missed = await fixture.ReadErrorAsync();
        Assert.Equal("Authoritative repair", missed.Item.Description);
        Assert.Equal("Bolt", (await fixture.GetAsync(id))!.Description);
        Assert.Equal(1, (await fixture.CompareAsync()).Different);
        await fixture.ExecuteSqlAsync(
            "DROP TRIGGER fail_reference ON purchasing.stock_item_references; DROP FUNCTION purchasing.fail_reference();"
        );
        await fixture.CompareAsync(repair: true);
        Assert.Equal("Authoritative repair", (await fixture.GetAsync(id))!.Description);
        await fixture.SendAsync(missed);
        await fixture.WaitForProcessedAsync(missed.MessageId);
        await fixture.SendAsync(missed);
        await fixture.WaitForProcessedAsync(missed.MessageId, 2);
        Assert.Equal("Authoritative repair", (await fixture.GetAsync(id))!.Description);
        Assert.Equal(2, (await fixture.StatusAsync()).SnapshotWatermark);
        Assert.True((await fixture.CompareAsync()).IsConsistent);
    }
}
