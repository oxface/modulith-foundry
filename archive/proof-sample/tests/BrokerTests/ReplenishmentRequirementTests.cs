using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Purchasing.Contracts;

namespace ModulithFoundry.BrokerTests;

public sealed class ReplenishmentRequirementTests
{
    [Theory]
    [InlineData("inventory", false, 4)]
    [InlineData("sales", true, 4)]
    [InlineData("sales", false, 0)]
    public async Task CreateRequirement_InvalidProvenanceCorrelationOrQuantity_GoesToOwnErrorQueue(
        string producer,
        bool wrongCorrelation,
        int quantity
    )
    {
        await using StockItemBootstrapFixture fixture =
            await StockItemBootstrapFixture.StartAsync();
        StockItemId item = await fixture.CreateAsync("BOLT");
        await fixture.BootstrapAsync();
        await fixture.StartErrorObserverAsync();
        var command = new CreateReplenishmentRequirementV1(
            Guid.CreateVersion7(),
            fixture.Actor.OrganizationId.Value,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            1,
            1,
            item.Value,
            quantity,
            "EA",
            DateTimeOffset.UtcNow
        );
        await fixture.SendAsync(command, producer, wrongCorrelation ? Guid.CreateVersion7() : null);
        Assert.Equal(command.MessageId, (await fixture.ReadRequestErrorAsync()).MessageId);
        Assert.Null(await fixture.RequestAsync(command.OperationId));
        Assert.Empty(
            Assert
                .IsType<ListReplenishmentRequirementsResult.Listed>(
                    await fixture.RequirementsAsync()
                )
                .Requirements
        );
    }

    [Fact]
    public async Task CreateRequirement_OutgoingInsertFails_RollsBackAndRedriveCreatesCompleteOutcome()
    {
        await using StockItemBootstrapFixture fixture =
            await StockItemBootstrapFixture.StartAsync();
        StockItemId item = await fixture.CreateAsync("BOLT");
        await fixture.BootstrapAsync();
        await fixture.StartCreatedObserverAsync();
        await fixture.StartErrorObserverAsync();
        await fixture.ExecuteSqlAsync(
            """
            CREATE FUNCTION purchasing.reject_test_outgoing() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'test outgoing insert failure'; END; $$;
            CREATE TRIGGER reject_test_outgoing BEFORE INSERT ON purchasing.outbox_messages
            FOR EACH ROW EXECUTE FUNCTION purchasing.reject_test_outgoing();
            """
        );
        var command = new CreateReplenishmentRequirementV1(
            Guid.CreateVersion7(),
            fixture.Actor.OrganizationId.Value,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            1,
            1,
            item.Value,
            4m,
            "EA",
            DateTimeOffset.UtcNow
        );
        await fixture.SendAsync(command);
        Assert.Equal(command.MessageId, (await fixture.ReadRequestErrorAsync()).MessageId);
        Assert.Null(await fixture.RequestAsync(command.OperationId));
        Assert.Empty(
            Assert
                .IsType<ListReplenishmentRequirementsResult.Listed>(
                    await fixture.RequirementsAsync()
                )
                .Requirements
        );
        await fixture.ExecuteSqlAsync(
            """
            DROP TRIGGER reject_test_outgoing ON purchasing.outbox_messages;
            DROP FUNCTION purchasing.reject_test_outgoing();
            """
        );
        await fixture.SendAsync(command);
        await fixture.WaitForProcessedAsync(command.MessageId);
        var created = await fixture.ReadCreatedAsync();
        var found = Assert.IsType<GetReplenishmentRequirementResult.Found>(
            await fixture.RequirementAsync(created.RequirementNumber)
        );
        Assert.Equal(command.OperationId, created.OperationId);
        Assert.Equal(created.RequirementId, found.Requirement.RequirementId);
        Assert.Single(
            Assert
                .IsType<ListReplenishmentRequirementsResult.Listed>(
                    await fixture.RequirementsAsync()
                )
                .Requirements
        );
    }

    [Fact]
    public async Task CreateRequirement_ConflictingOperation_GoesToPurchasingErrorQueueAndPreservesOriginal()
    {
        await using StockItemBootstrapFixture fixture =
            await StockItemBootstrapFixture.StartAsync();
        StockItemId item = await fixture.CreateAsync("BOLT");
        await fixture.BootstrapAsync();
        await fixture.StartErrorObserverAsync();
        var original = new CreateReplenishmentRequirementV1(
            Guid.CreateVersion7(),
            fixture.Actor.OrganizationId.Value,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            1,
            1,
            item.Value,
            4m,
            "EA",
            DateTimeOffset.UtcNow
        );
        await fixture.SendAsync(original);
        await fixture.WaitForProcessedAsync(original.MessageId);
        var conflicting = original with { MessageId = Guid.CreateVersion7(), Quantity = 5m };
        await fixture.SendAsync(conflicting);
        Assert.Equal(conflicting.MessageId, (await fixture.ReadRequestErrorAsync()).MessageId);
        Assert.Equal("completed", (await fixture.RequestAsync(original.OperationId))!.Status);
        var requirement = Assert.Single(
            Assert
                .IsType<ListReplenishmentRequirementsResult.Listed>(
                    await fixture.RequirementsAsync()
                )
                .Requirements
        );
        Assert.Equal(4m, requirement.Quantity);
    }

    [Fact]
    public async Task CreateRequirement_ForeignReference_RejectsInsteadOfWaitingOrCrossingTenant()
    {
        await using StockItemBootstrapFixture fixture =
            await StockItemBootstrapFixture.StartAsync();
        StockItemId item = await fixture.CreateAsync("BOLT");
        await fixture.BootstrapAsync();
        var otherOrganization = new ModulithFoundry.Modules.Access.Contracts.OrganizationId(
            Guid.CreateVersion7()
        );
        var command = new CreateReplenishmentRequirementV1(
            Guid.CreateVersion7(),
            otherOrganization.Value,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            1,
            1,
            item.Value,
            4m,
            "EA",
            DateTimeOffset.UtcNow
        );
        await fixture.SendAsync(command);
        await fixture.WaitForProcessedAsync(command.MessageId);
        Assert.Null(await fixture.RequestAsync(command.OperationId));
        var rejected = Assert.IsType<ReplenishmentRequestView>(
            await fixture.RequestAsync(command.OperationId, otherOrganization)
        );
        Assert.Equal("foreign-stock-item", rejected.ReasonCode);
        Assert.Null(rejected.RequirementId);
        Assert.Empty(
            Assert
                .IsType<ListReplenishmentRequirementsResult.Listed>(
                    await fixture.RequirementsAsync()
                )
                .Requirements
        );
    }

    [Fact]
    public async Task CreateRequirement_MissingProjectionRow_ResumesAfterExplicitReconciliation()
    {
        await using StockItemBootstrapFixture fixture =
            await StockItemBootstrapFixture.StartAsync();
        StockItemId item = await fixture.CreateAsync("BOLT");
        await fixture.BootstrapAsync();
        await fixture.StartCreatedObserverAsync();
        await fixture.ExecuteSqlAsync(
            $"DELETE FROM purchasing.stock_item_references WHERE stock_item_id = '{item.Value}';"
        );
        var command = new CreateReplenishmentRequirementV1(
            Guid.CreateVersion7(),
            fixture.Actor.OrganizationId.Value,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            1,
            1,
            item.Value,
            4m,
            "EA",
            DateTimeOffset.UtcNow
        );
        await fixture.SendAsync(command);
        await fixture.WaitForProcessedAsync(command.MessageId);
        Assert.Equal(
            "pending-reference",
            (await fixture.RequestAsync(command.OperationId))!.Status
        );
        await fixture.CompareAsync(repair: true);
        var created = await fixture.ReadCreatedAsync();
        Assert.Equal(
            created.RequirementId,
            (await fixture.RequestAsync(command.OperationId))!.RequirementId
        );
    }

    [Fact]
    public async Task CreateRequirement_CurrentReference_CreatesOneRequirementForRepeatedOperation()
    {
        await using StockItemBootstrapFixture fixture =
            await StockItemBootstrapFixture.StartAsync();
        StockItemId item = await fixture.CreateAsync("BOLT");
        await fixture.BootstrapAsync();
        await fixture.StartCreatedObserverAsync();
        await fixture.StartEndpointsAsync();
        var command = new CreateReplenishmentRequirementV1(
            Guid.CreateVersion7(),
            fixture.Actor.OrganizationId.Value,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            1,
            1,
            item.Value,
            4m,
            "EA",
            DateTimeOffset.UtcNow
        );
        await fixture.SendAsync(command);
        await fixture.WaitForProcessedAsync(command.MessageId);
        ReplenishmentRequestView request = Assert.IsType<ReplenishmentRequestView>(
            await fixture.RequestAsync(command.OperationId)
        );
        Assert.Equal("completed", request.Status);
        Assert.NotNull(request.RequirementId);
        ReplenishmentRequirementCreatedV1 created = await fixture.ReadCreatedAsync();
        Assert.Equal(request.RequirementId, created.RequirementId);
        Assert.Equal(command.MessageId, created.CausationId);
        Assert.Equal(command.OperationId, created.OperationId);
        Assert.Equal(4m, created.Quantity);
        Assert.Equal("EA", created.BaseUnitCode);
        Assert.True(created.RequirementNumber > 0);
        GetReplenishmentRequirementResult.Found found =
            Assert.IsType<GetReplenishmentRequirementResult.Found>(
                await fixture.RequirementAsync(created.RequirementNumber)
            );
        Assert.Equal("BOLT", found.Requirement.Sku);
        Assert.Equal(4m, found.Requirement.Quantity);
        Assert.Equal(created.RequirementId, found.Requirement.RequirementId);
        ListReplenishmentRequirementsResult.Listed listed =
            Assert.IsType<ListReplenishmentRequirementsResult.Listed>(
                await fixture.RequirementsAsync()
            );
        Assert.Single(listed.Requirements);
        var repeated = command with { MessageId = Guid.CreateVersion7() };
        await fixture.SendAsync(repeated);
        await fixture.WaitForProcessedAsync(repeated.MessageId);
        // The repeat is independent delivery, not a second business requirement.
        Assert.Equal(
            request.RequirementId,
            (await fixture.RequestAsync(command.OperationId))!.RequirementId
        );
        await fixture.SendAsync(command);
        await fixture.WaitForProcessedAsync(command.MessageId, count: 2);
        Assert.Single(
            Assert
                .IsType<ListReplenishmentRequirementsResult.Listed>(
                    await fixture.RequirementsAsync()
                )
                .Requirements
        );
    }

    [Fact]
    public async Task CreateRequirement_StaleReference_ResumesAfterRestartAndUsesCaughtUpSnapshot()
    {
        await using StockItemBootstrapFixture fixture =
            await StockItemBootstrapFixture.StartAsync();
        StockItemId item = await fixture.CreateAsync("BOLT");
        await fixture.BootstrapAsync();
        await fixture.StartCreatedObserverAsync();
        var command = new CreateReplenishmentRequirementV1(
            Guid.CreateVersion7(),
            fixture.Actor.OrganizationId.Value,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            1,
            1,
            item.Value,
            4m,
            "EA",
            DateTimeOffset.UtcNow,
            MinimumReferenceRevision: 2
        );
        await fixture.SendAsync(command);
        await fixture.WaitForProcessedAsync(command.MessageId);
        Assert.Equal(
            "pending-reference",
            (await fixture.RequestAsync(command.OperationId))!.Status
        );
        await fixture.RestartAsync();
        await fixture.ChangeDescriptionAsync("BOLT", "Updated bolt");
        var created = await fixture.ReadCreatedAsync();
        var found = Assert.IsType<GetReplenishmentRequirementResult.Found>(
            await fixture.RequirementAsync(created.RequirementNumber)
        );
        Assert.Equal("Updated bolt", found.Requirement.Description);
        Assert.Equal(2, found.Requirement.ReferenceRevision);
        Assert.Equal("completed", (await fixture.RequestAsync(command.OperationId))!.Status);
        Assert.IsType<GetReplenishmentRequirementResult.PermissionDenied>(
            await fixture.RequirementAsync(
                created.RequirementNumber,
                new ModulithFoundry.Modules.Access.Contracts.OrganizationId(Guid.CreateVersion7())
            )
        );
    }

    [Theory]
    [InlineData("KG", false, "base-unit-mismatch")]
    [InlineData("EA", true, "inactive-stock-item")]
    public async Task CreateRequirement_IneligibleReference_RejectsWithoutRequirement(
        string unit,
        bool inactive,
        string reason
    )
    {
        await using StockItemBootstrapFixture fixture =
            await StockItemBootstrapFixture.StartAsync();
        StockItemId item = await fixture.CreateAsync("BOLT");
        if (inactive)
            await fixture.SetActiveAsync("BOLT", false);
        await fixture.BootstrapAsync();
        var command = new CreateReplenishmentRequirementV1(
            Guid.CreateVersion7(),
            fixture.Actor.OrganizationId.Value,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            1,
            1,
            item.Value,
            4m,
            unit,
            DateTimeOffset.UtcNow
        );
        await fixture.SendAsync(command);
        await fixture.WaitForProcessedAsync(command.MessageId);
        var request = Assert.IsType<ReplenishmentRequestView>(
            await fixture.RequestAsync(command.OperationId)
        );
        Assert.Equal("rejected", request.Status);
        Assert.Equal(reason, request.ReasonCode);
        Assert.Null(request.RequirementId);
        Assert.Empty(
            Assert
                .IsType<ListReplenishmentRequirementsResult.Listed>(
                    await fixture.RequirementsAsync()
                )
                .Requirements
        );
    }

    [Fact]
    public async Task CreateRequirement_ProjectionNotReady_PersistsIntentAndResumesAfterBootstrap()
    {
        await using StockItemBootstrapFixture fixture =
            await StockItemBootstrapFixture.StartAsync();
        StockItemId item = await fixture.CreateAsync("BOLT");
        fixture.PauseSnapshotAfterWatermark();
        await fixture.StartEndpointsAsync();
        await fixture.WaitForSnapshotWatermarkAsync();
        var command = new CreateReplenishmentRequirementV1(
            Guid.CreateVersion7(),
            fixture.Actor.OrganizationId.Value,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            1,
            1,
            item.Value,
            4m,
            "EA",
            DateTimeOffset.UtcNow
        );
        try
        {
            await fixture.SendAsync(command);
            await fixture.WaitForProcessedAsync(command.MessageId);
            ReplenishmentRequestView pending = Assert.IsType<ReplenishmentRequestView>(
                await fixture.RequestAsync(command.OperationId)
            );
            Assert.Equal("pending-reference", pending.Status);
            Assert.Null(pending.RequirementId);
        }
        finally
        {
            fixture.ReleaseSnapshot();
        }
        await fixture.WaitForReadyAsync();
        for (
            int attempt = 0;
            attempt < 150
                && (await fixture.RequestAsync(command.OperationId))!.RequirementId is null;
            attempt++
        )
            await Task.Delay(TimeSpan.FromMilliseconds(100), fixture.CancellationToken);
        Assert.Equal("completed", (await fixture.RequestAsync(command.OperationId))!.Status);
        Assert.NotNull((await fixture.RequestAsync(command.OperationId))!.RequirementId);
    }
}
