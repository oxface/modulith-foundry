using Microsoft.Extensions.DependencyInjection;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Sales.Contracts;

namespace ModulithFoundry.BrokerTests;

public sealed class SalesCancellationTests
{
    [Fact]
    public async Task CancelOrder_RacingReservationSuccess_EventuallyReleasesAcceptedReservation()
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync(controlledOutcomes: true);
        var order = await fixture.ApproveAsync(4m);
        var reserve = await fixture.ReadCommandAsync();
        var reserved = new StockReservationOutcomeV1(
            Guid.CreateVersion7(),
            reserve.MessageId,
            reserve.OrganizationId,
            reserve.OperationId,
            reserve.ProcessId,
            reserve.OrderNumber,
            reserve.LineNumber,
            StockReservationOutcome.Reserved,
            Guid.CreateVersion7(),
            reserve.Quantity,
            6m,
            reserve.BaseUnitCode,
            null,
            DateTimeOffset.UtcNow
        );
        Task<CancelSalesOrderResult> cancellation = fixture.CancelAsync(order);
        await fixture.PublishAsync(reserved);
        var result = await cancellation;
        await fixture.WaitDeliveryAsync(reserved.MessageId);
        if (result is CancelSalesOrderResult.VersionConflict)
            result = await fixture.CancelAsync(order);
        Assert.IsType<CancelSalesOrderResult.Cancelled>(result);
        var release = await fixture.ReadReleaseCommandAsync();
        Assert.Equal(reserved.ReservationId, release.ReservationId);
        await fixture.PublishAsync(Released(release, 4m));
        await fixture.WaitAsync(order.OrderNumber, "compensated");
        Assert.Single(
            await fixture.ActivityAsync(order.OrderNumber),
            item => item.Kind == SalesOrderActivityKind.Cancelled
        );
        Assert.Single(
            await fixture.ActivityAsync(order.OrderNumber),
            item => item.Kind == SalesOrderActivityKind.CompensationCompleted
        );
    }

    [Fact]
    public async Task CancelOrder_ExistingReplenishmentRequirement_ReleasesStockButRetainsPurchasingFact()
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync(enablePurchasing: true);
        var order = await fixture.ApproveAsync(2m, 20m);
        var before = await fixture.WaitForRequirementAsync(order.OrderNumber);
        var requirement = Assert.Single(
            before.Lines,
            line => line.ReplenishmentRequirementNumber.HasValue
        );
        Assert.IsType<CancelSalesOrderResult.Cancelled>(await fixture.CancelAsync(order));
        var completed = await fixture.WaitAsync(order.OrderNumber, "compensated");
        Assert.Equal(0m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(
            requirement.ReplenishmentRequirementId,
            Assert
                .Single(completed.Lines, line => line.ReplenishmentRequirementNumber.HasValue)
                .ReplenishmentRequirementId
        );
        Assert.IsType<ModulithFoundry.Modules.Purchasing.Contracts.GetReplenishmentRequirementResult.Found>(
            await fixture.RequirementAsync(requirement.ReplenishmentRequirementNumber!.Value)
        );
    }

    [Fact]
    public async Task CancelOrder_CurrentPermissionAndInputFailures_DoNotChangeDemand()
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync();
        var order = await fixture.SubmitAsync(4m);
        var denied = await fixture.RunAsync(
            fixture.Approver,
            services =>
                services
                    .GetRequiredService<ISalesOrderCancellation>()
                    .CancelAsync(
                        new(
                            fixture.Approver.UserId,
                            fixture.Approver.OrganizationId,
                            order.OrderNumber,
                            order.Version,
                            "Customer withdrew"
                        ),
                        fixture.CancellationToken
                    )
        );
        Assert.IsType<CancelSalesOrderResult.PermissionDenied>(denied);
        Assert.IsType<CancelSalesOrderResult.InvalidReason>(await fixture.CancelAsync(order, " "));
        Assert.IsType<CancelSalesOrderResult.VersionConflict>(
            await fixture.CancelAsync(order with { Version = 1 })
        );
        Assert.IsType<CancelSalesOrderResult.NotFound>(
            await fixture.CancelAsync(order with { OrderNumber = long.MaxValue })
        );
        Assert.Equal(
            SalesOrderStatus.AwaitingApproval,
            (await fixture.ReadOrderAsync(order.OrderNumber)).Status
        );
        Assert.IsType<CancelSalesOrderResult.Cancelled>(await fixture.CancelAsync(order));
        Assert.Equal(
            SalesOrderStatus.Cancelled,
            (await fixture.ReadOrderAsync(order.OrderNumber)).Status
        );
        Assert.Equal(0m, (await fixture.StockAsync()).ReservedQuantity);
    }

    [Fact]
    public async Task CancelOrder_BeforeMainDiscovery_RemainsCompensatedAfterRestart()
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync(createMain: false);
        var order = await fixture.ApproveAsync(4m);
        Assert.Equal(
            OrderFulfilmentStatus.PendingDispatch,
            (await fixture.ReadAsync(order.OrderNumber)).Status
        );
        Assert.IsType<CancelSalesOrderResult.Cancelled>(await fixture.CancelAsync(order));
        var completed = await fixture.WaitAsync(order.OrderNumber, "compensated");
        await fixture.RestartAsync(addMain: true);
        var marker = await fixture.ApproveAsync(1m);
        await fixture.WaitAsync(marker.OrderNumber, "reserved");
        var retained = await fixture.ReadAsync(order.OrderNumber);
        Assert.Equal(completed.Version, retained.Version);
        Assert.All(retained.Lines, line => Assert.Equal(0, line.AttemptCount));
        Assert.Equal(OrderFulfilmentStatus.Compensated, retained.Status);
        Assert.Equal(1m, (await fixture.StockAsync()).ReservedQuantity);
    }

    [Fact]
    public async Task CancelOrder_CompetingRequests_CommitOneCancellationAndRelease()
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync();
        var order = await fixture.ApproveAsync(4m);
        await fixture.WaitAsync(order.OrderNumber, "reserved");
        var results = await Task.WhenAll(fixture.CancelAsync(order), fixture.CancelAsync(order));
        Assert.Single(results, result => result is CancelSalesOrderResult.Cancelled);
        Assert.All(
            results,
            result =>
                Assert.True(
                    result
                        is CancelSalesOrderResult.Cancelled
                            or CancelSalesOrderResult.Unchanged
                            or CancelSalesOrderResult.VersionConflict
                )
        );
        await fixture.WaitAsync(order.OrderNumber, "compensated");
        Assert.Equal(0m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(4, (await fixture.StockAsync()).Version);
        Assert.Single(
            await fixture.ActivityAsync(order.OrderNumber),
            item => item.Kind == SalesOrderActivityKind.Cancelled
        );
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CancelOrder_ReservationOutcomeArrivesLater_WaitsAndCompensatesOnlySuccess(
        bool reserved
    )
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync(controlledOutcomes: true);
        var order = await fixture.ApproveAsync(4m);
        var original = await fixture.ReadCommandAsync();
        Assert.IsType<CancelSalesOrderResult.Cancelled>(await CancelAsync(fixture, order));
        Assert.Equal(
            OrderFulfilmentStatus.CompensationPending,
            (await fixture.ReadAsync(order.OrderNumber)).Status
        );
        var reservation = new StockReservationOutcomeV1(
            Guid.CreateVersion7(),
            original.MessageId,
            original.OrganizationId,
            original.OperationId,
            original.ProcessId,
            original.OrderNumber,
            original.LineNumber,
            reserved ? StockReservationOutcome.Reserved : StockReservationOutcome.Shortage,
            reserved ? Guid.CreateVersion7() : null,
            original.Quantity,
            reserved ? 6m : 0m,
            original.BaseUnitCode,
            reserved ? null : "insufficient-stock",
            DateTimeOffset.UtcNow
        );
        await fixture.PublishAsync(reservation);
        await fixture.WaitDeliveryAsync(reservation.MessageId);
        if (reserved)
        {
            var release = await fixture.ReadReleaseCommandAsync();
            Assert.Equal(original.OperationId, release.ReservationOperationId);
            Assert.Equal(reservation.ReservationId, release.ReservationId);
            Assert.Equal(original.ProcessId, release.ProcessId);
            Assert.NotEqual(original.OperationId, release.OperationId);
            Assert.Equal(
                OrderFulfilmentStatus.CompensationPending,
                (await fixture.ReadAsync(order.OrderNumber)).Status
            );
            await fixture.PublishAsync(Released(release, original.Quantity));
        }
        var completed = await fixture.WaitAsync(order.OrderNumber, "compensated");
        Assert.Equal(
            reserved ? OrderFulfilmentReleaseStatus.Released : null,
            Assert.Single(completed.Lines).ReleaseStatus
        );
        Assert.Null(Assert.Single(completed.Lines).ReplenishmentQuantity);
        Assert.Equal(
            SalesOrderStatus.Cancelled,
            (await fixture.ReadOrderAsync(order.OrderNumber)).Status
        );
        var replay = reservation with { MessageId = Guid.CreateVersion7() };
        await fixture.PublishAsync(replay);
        await fixture.WaitDeliveryAsync(replay.MessageId);
        Assert.Equal(completed.Version, (await fixture.ReadAsync(order.OrderNumber)).Version);
    }

    [Fact]
    public async Task CancelOrder_RepeatedRequest_RetainsFirstReasonAndDoesNotRepeatCompensation()
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync();
        var order = await fixture.ApproveAsync(4m);
        await fixture.WaitAsync(order.OrderNumber, "reserved");
        Assert.IsType<CancelSalesOrderResult.Cancelled>(await CancelAsync(fixture, order));
        var completed = await fixture.WaitAsync(order.OrderNumber, "compensated");
        var before = await fixture.StockAsync();
        var activity = await fixture.ActivityAsync(order.OrderNumber);
        var retry = Assert
            .IsType<CancelSalesOrderResult.Unchanged>(
                await CancelAsync(fixture, order, "Different retry reason")
            )
            .Order;
        Assert.Equal("Customer withdrew", retry.CancellationReason);
        Assert.Equal(before, await fixture.StockAsync());
        Assert.Equal(completed.Version, (await fixture.ReadAsync(order.OrderNumber)).Version);
        Assert.Equal(activity.Count, (await fixture.ActivityAsync(order.OrderNumber)).Count);
    }

    [Fact]
    public async Task CancelOrder_ReservedLines_ReleasesStockAndCompletesCompensation()
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync();
        var order = await fixture.ApproveAsync(2m, 3m);
        await fixture.WaitAsync(order.OrderNumber, "reserved");
        var result = await fixture.RunAsync(
            fixture.Administrator,
            services =>
                services
                    .GetRequiredService<ISalesOrderCancellation>()
                    .CancelAsync(
                        new(
                            fixture.Administrator.UserId,
                            fixture.Administrator.OrganizationId,
                            order.OrderNumber,
                            order.Version,
                            "Customer withdrew the request"
                        ),
                        fixture.CancellationToken
                    )
        );
        var cancelled = Assert.IsType<CancelSalesOrderResult.Cancelled>(result).Order;
        Assert.Equal(SalesOrderStatus.Cancelled, cancelled.Status);
        Assert.Equal("Customer withdrew the request", cancelled.CancellationReason);
        var process = await fixture.WaitAsync(order.OrderNumber, "compensated");
        Assert.All(
            process.Lines,
            line => Assert.Equal(OrderFulfilmentReleaseStatus.Released, line.ReleaseStatus)
        );
        Assert.Equal(0m, (await fixture.StockAsync()).ReservedQuantity);
        Assert.Equal(10m, (await fixture.StockAsync()).OnHandQuantity);
        var activity = await fixture.ActivityAsync(order.OrderNumber);
        Assert.Single(activity, item => item.Kind == SalesOrderActivityKind.Cancelled);
        Assert.Equal(
            2,
            activity.Count(item => item.Kind == SalesOrderActivityKind.ReservationReleased)
        );
        Assert.Single(activity, item => item.Kind == SalesOrderActivityKind.CompensationCompleted);
    }

    private static Task<CancelSalesOrderResult> CancelAsync(
        SalesFulfilmentFixture fixture,
        SalesOrderView order,
        string reason = "Customer withdrew"
    ) =>
        fixture.RunAsync(
            fixture.Administrator,
            services =>
                services
                    .GetRequiredService<ISalesOrderCancellation>()
                    .CancelAsync(
                        new(
                            fixture.Administrator.UserId,
                            fixture.Administrator.OrganizationId,
                            order.OrderNumber,
                            order.Version,
                            reason
                        ),
                        fixture.CancellationToken
                    )
        );

    private static StockReservationReleaseOutcomeV1 Released(
        ReleaseReservationV1 command,
        decimal quantity
    ) =>
        new(
            Guid.CreateVersion7(),
            command.MessageId,
            command.OrganizationId,
            command.OperationId,
            command.ProcessId,
            command.OrderNumber,
            command.LineNumber,
            command.ReservationOperationId,
            command.ReservationId,
            StockReservationReleaseOutcome.Released,
            quantity,
            "EA",
            null,
            DateTimeOffset.UtcNow
        );
}
