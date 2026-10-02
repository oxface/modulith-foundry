using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Purchasing.Contracts;

namespace ModulithFoundry.BrokerTests;

public sealed class StockItemBootstrapTests
{
    [Fact]
    public async Task Publication_ReusedScopeWithInterleavedWriter_KeepsUniqueCommittedRevisions()
    {
        await using StockItemBootstrapFixture fixture =
            await StockItemBootstrapFixture.StartAsync();
        await fixture.InterleaveCreatesInReusedScopeAsync();
        StockItemSnapshotV1 snapshot = await fixture.ExportAsync();
        Assert.Equal(3, snapshot.HighWatermark);
        Assert.Equal([1L, 2L, 3L], snapshot.Items.Select(x => x.Revision).Order().ToArray());
    }

    [Fact]
    public async Task Publication_ReusedScopeAfterAnotherWriter_PublishesFreshCompleteState()
    {
        await using StockItemBootstrapFixture fixture =
            await StockItemBootstrapFixture.StartAsync();
        StockItemId id = await fixture.InterleaveChangesInReusedScopeAsync();
        await fixture.WaitForDescriptionAsync(id, "Fresh description");
        StockItemReferenceStateV1 authoritative = Assert.Single(
            (await fixture.ExportAsync()).Items
        );
        // Wait for the deactivation publication, not merely the preceding description event.
        for (int attempt = 0; attempt < 100 && (await fixture.GetAsync(id))!.IsActive; attempt++)
            await Task.Delay(TimeSpan.FromMilliseconds(100), fixture.CancellationToken);
        StockItemProjectionView projection = Assert.IsType<StockItemProjectionView>(
            await fixture.GetAsync(id)
        );
        Assert.False(projection.IsActive);
        Assert.Equal(authoritative.Description, projection.Description);
        Assert.Equal(3, projection.SourceRevision);
    }

    [Fact]
    public async Task Export_WriterCommitsBetweenQueries_ReturnsOneConsistentCommittedPrefix()
    {
        await using StockItemBootstrapFixture fixture =
            await StockItemBootstrapFixture.StartAsync();
        StockItemId id = await fixture.CreateAsync("BOLT");
        fixture.PauseSnapshotAfterWatermark();
        Task<StockItemSnapshotV1> exporting = Task.Run(
            fixture.ExportAsync,
            fixture.CancellationToken
        );
        await fixture.WaitForSnapshotWatermarkAsync();
        try
        {
            await fixture.ChangeDescriptionAsync("BOLT", "New description");
            await fixture.CreateAsync("NUT");
        }
        finally
        {
            fixture.ReleaseSnapshot();
        }
        StockItemSnapshotV1 snapshot = await exporting;
        Assert.Equal(1, snapshot.HighWatermark);
        StockItemReferenceStateV1 item = Assert.Single(snapshot.Items);
        Assert.Equal(id.Value, item.StockItemId);
        Assert.Equal("Bolt", item.Description);
        Assert.Equal(1, item.Revision);
        Assert.Equal(3, (await fixture.ExportAsync()).HighWatermark);
    }

    [Fact]
    public async Task Tail_DuplicateAndReorderedFullStates_PreservesLatestPerItemAndTenantIsolation()
    {
        await using StockItemBootstrapFixture fixture =
            await StockItemBootstrapFixture.StartAsync();
        StockItemId id = await fixture.CreateAsync("BOLT");
        await fixture.BootstrapAsync();
        StockItemReferenceStateV1 state = new(
            fixture.Actor.OrganizationId.Value,
            id.Value,
            "BOLT",
            "Latest",
            "EA",
            true,
            5
        );
        StockItemReferenceChangedV1 newest = new(
            Guid.CreateVersion7(),
            state,
            DateTimeOffset.UtcNow
        );
        await fixture.SendAsync(newest);
        await fixture.WaitForProcessedAsync(newest.MessageId);
        await fixture.SendAsync(newest);
        await fixture.WaitForProcessedAsync(newest.MessageId, 2);
        StockItemReferenceChangedV1 repeatedSemanticState = newest with
        {
            MessageId = Guid.CreateVersion7(),
        };
        await fixture.SendAsync(repeatedSemanticState);
        await fixture.WaitForProcessedAsync(repeatedSemanticState.MessageId);
        StockItemReferenceChangedV1 old = new(
            Guid.CreateVersion7(),
            state with
            {
                Description = "Older",
                Revision = 3,
            },
            DateTimeOffset.UtcNow
        );
        await fixture.SendAsync(old);
        await fixture.WaitForProcessedAsync(old.MessageId);
        var otherOrganization = new OrganizationId(Guid.CreateVersion7());
        StockItemReferenceChangedV1 otherItem = new(
            Guid.CreateVersion7(),
            state with
            {
                OrganizationId = otherOrganization.Value,
                StockItemId = Guid.CreateVersion7(),
                Sku = "NUT",
                Description = "Other item",
                Revision = 4,
            },
            DateTimeOffset.UtcNow
        );
        await fixture.SendAsync(otherItem);
        await fixture.WaitForProcessedAsync(otherItem.MessageId);
        Assert.Equal("Latest", (await fixture.GetAsync(id))!.Description);
        Assert.Equal(5, (await fixture.GetAsync(id))!.SourceRevision);
        Assert.NotNull(await fixture.GetAsync(otherOrganization, otherItem.Item.StockItemId));
        Assert.Null(
            await fixture.GetAsync(fixture.Actor.OrganizationId, otherItem.Item.StockItemId)
        );
        Assert.Equal(1, (await fixture.StatusAsync()).SnapshotWatermark);
    }

    [Fact]
    public async Task Publication_AuditInsertFails_RollsBackReferenceAndCursorBeforeRetry()
    {
        await using StockItemBootstrapFixture fixture =
            await StockItemBootstrapFixture.StartAsync();
        await fixture.ExecuteSqlAsync(
            """
            CREATE FUNCTION inventory.test_fail_reference_audit() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'test audit failure'; END; $$;
            CREATE TRIGGER test_fail_reference_audit BEFORE INSERT ON inventory.audit_entries
            FOR EACH ROW EXECUTE FUNCTION inventory.test_fail_reference_audit();
            """
        );
        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.CreateAsync("BOLT"));
        StockItemSnapshotV1 empty = await fixture.ExportAsync();
        Assert.Equal(0, empty.HighWatermark);
        Assert.Empty(empty.Items);
        await fixture.ExecuteSqlAsync(
            "DROP TRIGGER test_fail_reference_audit ON inventory.audit_entries; DROP FUNCTION inventory.test_fail_reference_audit();"
        );
        StockItemId id = await fixture.CreateAsync("BOLT");
        await fixture.BootstrapAsync();
        Assert.Equal(1, (await fixture.GetAsync(id))!.SourceRevision);
    }

    [Fact]
    public async Task Bootstrap_WritesCommitDuringSnapshot_PreservesConsistentBoundaryAndCompleteTail()
    {
        await using StockItemBootstrapFixture fixture =
            await StockItemBootstrapFixture.StartAsync();
        StockItemId original = await fixture.CreateAsync("BOLT");
        fixture.PauseSnapshotAfterWatermark();
        await fixture.StartEndpointsAsync();
        await fixture.WaitForSnapshotWatermarkAsync();
        try
        {
            await fixture.ChangeDescriptionAsync("BOLT", "Changed during snapshot");
            StockItemId added = await fixture.CreateAsync("NUT");
            Assert.False((await fixture.StatusAsync()).IsReady);
            Assert.Null(await fixture.GetAsync(original));
            fixture.ReleaseSnapshot();
            await fixture.WaitForReadyAsync();
            await fixture.WaitForDescriptionAsync(original, "Changed during snapshot");
            await fixture.WaitForDescriptionAsync(added, "Bolt");
            Assert.Equal(1, (await fixture.StatusAsync()).SnapshotWatermark);
            Assert.Equal(2, (await fixture.GetAsync(original))!.SourceRevision);
            Assert.Equal(3, (await fixture.GetAsync(added))!.SourceRevision);
        }
        finally
        {
            fixture.ReleaseSnapshot();
        }
    }

    [Fact]
    public async Task Tail_RestartWithCommittedProjection_ResumesWithoutReplacingSnapshotCheckpoint()
    {
        await using StockItemBootstrapFixture fixture =
            await StockItemBootstrapFixture.StartAsync();
        StockItemId id = await fixture.CreateAsync("BOLT");
        await fixture.BootstrapAsync();
        await fixture.ChangeDescriptionAsync("BOLT", "Before restart");
        await fixture.WaitForDescriptionAsync(id, "Before restart");
        await fixture.RestartAsync();
        await fixture.ChangeDescriptionAsync("BOLT", "After restart");
        await fixture.WaitForDescriptionAsync(id, "After restart");
        Assert.Equal(1, (await fixture.StatusAsync()).SnapshotWatermark);
        Assert.Equal(3, (await fixture.GetAsync(id))!.SourceRevision);
    }

    [Fact]
    public async Task Tail_DescriptionChangedAfterBootstrap_AdvancesPurchasingReference()
    {
        await using StockItemBootstrapFixture fixture =
            await StockItemBootstrapFixture.StartAsync();
        StockItemId id = await fixture.CreateAsync("BOLT");
        await fixture.StartEndpointsAsync();
        await fixture.BootstrapAsync();
        await fixture.ChangeDescriptionAsync("BOLT", "New bolt");
        await fixture.WaitForDescriptionAsync(id, "New bolt");
        Assert.Equal(2, (await fixture.GetAsync(id))!.SourceRevision);
    }

    [Fact]
    public async Task Export_CommittedCreationAndDeactivation_ReturnsMatchingRevisionAndWatermark()
    {
        await using StockItemBootstrapFixture fixture =
            await StockItemBootstrapFixture.StartAsync();
        StockItemId id = await fixture.CreateAsync("BOLT");
        await fixture.SetActiveAsync("BOLT", false);
        StockItemSnapshotV1 snapshot = await fixture.ExportAsync();
        Assert.Equal(2, snapshot.HighWatermark);
        StockItemReferenceStateV1 item = Assert.Single(snapshot.Items);
        Assert.Equal(id.Value, item.StockItemId);
        Assert.Equal(2, item.Revision);
        Assert.False(item.IsActive);
    }

    [Fact]
    public async Task Bootstrap_PreExistingInactiveItem_InstallsCompleteTenantReference()
    {
        await using StockItemBootstrapFixture fixture =
            await StockItemBootstrapFixture.StartAsync();
        StockItemId id = await fixture.CreateAsync("BOLT");
        await fixture.SetActiveAsync("BOLT", false);
        await fixture.PublishHistoryBeforePurchasingSubscribesAsync();
        await fixture.BootstrapAsync();
        StockItemProjectionView item = Assert.IsType<StockItemProjectionView>(
            await fixture.GetAsync(id)
        );
        Assert.Equal("BOLT", item.Sku);
        Assert.Equal("Bolt", item.Description);
        Assert.Equal("EA", item.BaseUnitCode);
        Assert.False(item.IsActive);
        Assert.Equal(fixture.Actor.OrganizationId.Value, item.OrganizationId);
        Assert.True((await fixture.StatusAsync()).IsReady);
    }
}
