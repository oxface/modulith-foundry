using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Sales.Contracts;

namespace ModulithFoundry.BrokerTests;

public sealed class SalesCancellationAtomicityTests
{
    [Theory]
    [InlineData("orders")]
    [InlineData("fulfilment_processes")]
    [InlineData("fulfilment_lines")]
    [InlineData("order_activity")]
    [InlineData("audit_entries")]
    [InlineData("outbox_messages")]
    public async Task CancelOrder_AtomicParticipantFails_RollsBackIntentAndCanRetry(string table)
    {
        await using var fixture = await SalesFulfilmentFixture.StartAsync();
        var order = await fixture.ApproveAsync(4m);
        var before = await fixture.WaitAsync(order.OrderNumber, "reserved");
        var activity = await fixture.ActivityAsync(order.OrderNumber);
        await fixture.ExecuteSetupAsync(
            $$"""
            CREATE FUNCTION sales.reject_cancel() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'test cancellation atomicity fault'; END $$;
            CREATE TRIGGER reject_cancel BEFORE INSERT OR UPDATE ON sales.{{table}}
            FOR EACH ROW EXECUTE FUNCTION sales.reject_cancel();
            """
        );
        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.CancelAsync(order));
        Assert.Equal(
            SalesOrderStatus.Approved,
            (await fixture.ReadOrderAsync(order.OrderNumber)).Status
        );
        Assert.Equal(before.Version, (await fixture.ReadAsync(order.OrderNumber)).Version);
        Assert.Null(
            Assert.Single((await fixture.ReadAsync(order.OrderNumber)).Lines).ReleaseStatus
        );
        Assert.Equal(activity.Count, (await fixture.ActivityAsync(order.OrderNumber)).Count);
        Assert.Equal(4m, (await fixture.StockAsync()).ReservedQuantity);
        await fixture.ExecuteSetupAsync(
            $"DROP TRIGGER reject_cancel ON sales.{table}; DROP FUNCTION sales.reject_cancel();"
        );
        Assert.IsType<CancelSalesOrderResult.Cancelled>(await fixture.CancelAsync(order));
        await fixture.WaitAsync(order.OrderNumber, "compensated");
        Assert.Equal(0m, (await fixture.StockAsync()).ReservedQuantity);
    }
}
