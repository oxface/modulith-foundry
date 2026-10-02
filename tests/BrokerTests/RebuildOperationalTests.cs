using Microsoft.EntityFrameworkCore;

namespace ModulithFoundry.BrokerTests;

public sealed class RebuildOperationalTests
{
    [Fact]
    public async Task Rebuild_WriteFails_ReportsFailureAndRollsBackUntilExplicitRetry()
    {
        using var telemetry = new MessagingTelemetryProbe();
        await using var fixture = await ReservationFixture.StartAsync(logs: telemetry);
        telemetry.Observe(fixture.MeterFactory);
        var position = await fixture.StockAsync();
        await fixture.ExecuteSqlAsync(
            """
            UPDATE inventory.stock_position_current SET on_hand_quantity = 11, available_quantity = 11;
            CREATE FUNCTION inventory.reject_test_rebuild() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'private-rebuild-content'; END $$;
            CREATE TRIGGER reject_test_rebuild BEFORE UPDATE ON inventory.stock_position_current
            FOR EACH ROW EXECUTE FUNCTION inventory.reject_test_rebuild();
            """
        );
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            fixture.RebuildAsync(position.StockPositionId, false)
        );
        Assert.Equal(
            1,
            telemetry.Counter(
                "modulith_foundry.inventory.stock_position_projection.rebuild_failures"
            )
        );
        Assert.Equal(11m, (await fixture.StockAsync()).OnHandQuantity);
        Assert.Equal(position.Version, (await fixture.StockAsync()).Version);
        await fixture.ExecuteSqlAsync(
            """
            DROP TRIGGER reject_test_rebuild ON inventory.stock_position_current;
            DROP FUNCTION inventory.reject_test_rebuild();
            """
        );
        await fixture.RebuildAsync(position.StockPositionId, false);
        Assert.Equal(10m, (await fixture.StockAsync()).OnHandQuantity);
        Assert.Equal(position.Version, (await fixture.StockAsync()).Version);
        Assert.Equal(
            1,
            telemetry.Counter(
                "modulith_foundry.inventory.stock_position_projection.rebuild_failures"
            )
        );
        Assert.Empty(telemetry.Tags);
        Assert.Contains(
            telemetry.Logs,
            message => message.Contains("rebuild failed", StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            telemetry.Logs,
            message => message.Contains("private-rebuild-content", StringComparison.Ordinal)
        );
    }
}
