using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Contracts;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ModulithFoundry.PersistenceTests;

public sealed partial class StockPositionPersistenceTests
{
    [Theory]
    [InlineData(1_000, false)]
    [InlineData(10_000, false)]
    [InlineData(1_000, true)]
    [InlineData(10_000, true)]
    public async Task Rebuild_LargeRetainedHistory_RestoresExpectedStateAndAllowsNextAppend(
        int additionalEvents,
        bool reservationHistory
    )
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        var organization = CreateOrganizationContext();
        await using var services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization
        );
        await CreateReferenceDataAsync(services, organization);
        var original = Assert
            .IsType<RecordStockReceiptResult.Recorded>(
                await RecordReceiptAsync(services, organization, 10m, expectedVersion: 0)
            )
            .Position;
        await SeedRetainedHistoryAsync(
            postgres.GetConnectionString(),
            original.StockPositionId.Value,
            additionalEvents,
            reservationHistory
        );

        // Warm the normal read path, not the measured full replay or repair.
        await ReadAtVersionAsync(services, organization, 2);
        long head = additionalEvents + 2L;
        decimal expectedOnHand = reservationHistory ? 10m : additionalEvents + 10m;
        string shape = reservationHistory ? "reserve-release" : "receipts";
        for (int sample = 1; sample <= 3; sample++)
        {
            var historical = await MeasureReplayAsync(
                $"live {shape} events={head} sample={sample}",
                () => ReadAtVersionAsync(services, organization, head)
            );
            Assert.Equal(head, historical.Version);
            Assert.Equal(expectedOnHand, historical.OnHandQuantity);
            Assert.Equal(0m, historical.ReservedQuantity);
            Assert.Equal(expectedOnHand, historical.AvailableQuantity);
        }

        var rebuilt = Assert.IsType<StockPositionRebuildResult.Rebuilt>(
            await MeasureReplayAsync(
                $"repair {shape} events={head}",
                () => RebuildProjectionAsync(services, organization, original.StockPositionId)
            )
        );
        Assert.False(rebuilt.PreviousModelMatched);
        Assert.Equal(head, rebuilt.Version);
        var current = await ReadCurrentPositionAsync(services, organization);
        Assert.Equal(original.StockPositionId, current.StockPositionId);
        Assert.Equal(head, current.Version);
        Assert.Equal(expectedOnHand, current.OnHandQuantity);
        Assert.Equal(0m, current.ReservedQuantity);
        Assert.Equal(expectedOnHand, current.AvailableQuantity);
        Assert.True(
            Assert
                .IsType<StockPositionRebuildResult.Rebuilt>(
                    await MeasureReplayAsync(
                        $"verify {shape} events={head}",
                        () =>
                            RebuildProjectionAsync(services, organization, original.StockPositionId)
                    )
                )
                .PreviousModelMatched
        );
        var appended = Assert
            .IsType<RecordStockReceiptResult.Recorded>(
                await MeasureReplayAsync(
                    $"inline append {shape} retained-children={(reservationHistory ? additionalEvents / 2 : 0)}",
                    () => RecordReceiptAsync(services, organization, 1m, head)
                )
            )
            .Position;
        Assert.Equal(head + 1, appended.Version);
        Assert.Equal(expectedOnHand + 1m, appended.OnHandQuantity);
        Assert.Equal(0m, appended.ReservedQuantity);
    }

    [Fact]
    public async Task Rebuild_InProgress_BlocksOtherPositionAndCreationButNotReadersOrOtherOrganization()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18.6").Build();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        var organization = CreateOrganizationContext();
        var foreign = CreateOrganizationContext();
        await using var services = await CreateServicesAsync(
            postgres.GetConnectionString(),
            organization
        );
        await using var foreignServices = await CreateServicesAsync(
            postgres.GetConnectionString(),
            foreign,
            migrate: false
        );
        await CreateReferenceDataAsync(services, organization);
        await CreateReferenceDataAsync(foreignServices, foreign);
        await using (var scope = services.CreateAsyncScope())
        {
            var locations =
                scope.ServiceProvider.GetRequiredService<IStockingLocationAdministration>();
            foreach (string code in new[] { "SECOND", "THIRD" })
                Assert.IsType<CreateStockingLocationResult.Created>(
                    await locations.CreateAsync(
                        new(organization.UserId, organization.OrganizationId, code, code),
                        TestContext.Current.CancellationToken
                    )
                );
        }
        var original = Assert
            .IsType<RecordStockReceiptResult.Recorded>(
                await RecordReceiptAsync(services, organization, 10m, 0)
            )
            .Position;
        Assert.IsType<RecordStockReceiptResult.Recorded>(
            await RecordAtLocationAsync(services, organization, "SECOND", 0)
        );
        await InstallProjectionPauseAsync(postgres.GetConnectionString());
        await using var blocker = new NpgsqlConnection(postgres.GetConnectionString());
        await blocker.OpenAsync(TestContext.Current.CancellationToken);
        await using var barrier = await blocker.BeginTransactionAsync(
            TestContext.Current.CancellationToken
        );
        await using var hold = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(731025)",
            blocker,
            barrier
        );
        await hold.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        await using var repairer = await CreateNamedServicesAsync(
            postgres.GetConnectionString(),
            organization,
            "gate-repair"
        );
        await using var otherWriter = await CreateNamedServicesAsync(
            postgres.GetConnectionString(),
            organization,
            "gate-other-position"
        );
        await using var creator = await CreateNamedServicesAsync(
            postgres.GetConnectionString(),
            organization,
            "gate-new-position"
        );
        Task<StockPositionRebuildResult> repair = RebuildProjectionAsync(
            repairer,
            organization,
            original.StockPositionId
        );
        await WaitForAdvisoryWaitAsync(postgres.GetConnectionString(), "gate-repair");
        Task<RecordStockReceiptResult> other = RecordAtLocationAsync(
            otherWriter,
            organization,
            "SECOND",
            2
        );
        Task<RecordStockReceiptResult> creation = RecordAtLocationAsync(
            creator,
            organization,
            "THIRD",
            0
        );
        await WaitForAdvisoryWaitAsync(postgres.GetConnectionString(), "gate-other-position");
        await WaitForAdvisoryWaitAsync(postgres.GetConnectionString(), "gate-new-position");

        // Bound progress assertions: a regression must fail rather than hang behind our own barrier.
        var readable = await ReadCurrentPositionAsync(services, organization)
            .WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        Assert.Equal(original, readable);
        var foreignResult = await RecordReceiptAsync(foreignServices, foreign, 4m, 0)
            .WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        Assert.Equal(
            4m,
            Assert.IsType<RecordStockReceiptResult.Recorded>(foreignResult).Position.OnHandQuantity
        );
        Assert.False(other.IsCompleted);
        Assert.False(creation.IsCompleted);
        await barrier.CommitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, Assert.IsType<StockPositionRebuildResult.Rebuilt>(await repair).Version);
        Assert.Equal(
            3,
            Assert.IsType<RecordStockReceiptResult.Recorded>(await other).Position.Version
        );
        Assert.Equal(
            2,
            Assert.IsType<RecordStockReceiptResult.Recorded>(await creation).Position.Version
        );
    }

    private static async Task<RecordStockReceiptResult> RecordAtLocationAsync(
        ServiceProvider services,
        OrganizationAccessContext organization,
        string location,
        long version
    )
    {
        await using var scope = services.CreateAsyncScope();
        return await scope
            .ServiceProvider.GetRequiredService<IStockPositionOperations>()
            .RecordReceiptAsync(
                new(
                    organization.UserId,
                    organization.OrganizationId,
                    location,
                    "bolt-01",
                    1m,
                    version
                ),
                TestContext.Current.CancellationToken
            );
    }

    private static async Task<StockPositionView> ReadAtVersionAsync(
        ServiceProvider services,
        OrganizationAccessContext organization,
        long version
    )
    {
        await using var scope = services.CreateAsyncScope();
        return Assert
            .IsType<GetStockPositionResult.Found>(
                await scope
                    .ServiceProvider.GetRequiredService<IStockPositionOperations>()
                    .GetAtVersionAsync(
                        new(
                            organization.UserId,
                            organization.OrganizationId,
                            "main",
                            "bolt-01",
                            version
                        ),
                        TestContext.Current.CancellationToken
                    )
            )
            .Position;
    }

    private static async Task<T> MeasureReplayAsync<T>(string label, Func<Task<T>> operation)
    {
        long before = GC.GetTotalAllocatedBytes(precise: true);
        var timer = Stopwatch.StartNew();
        T result = await operation();
        timer.Stop();
        long allocated = GC.GetTotalAllocatedBytes(precise: true) - before;
        TestContext.Current.TestOutputHelper!.WriteLine(
            $"REPLAY-COST {label}: elapsed-ms={timer.Elapsed.TotalMilliseconds:F1}, process-allocated-bytes={allocated}"
        );
        return result;
    }

    private static Task SeedRetainedHistoryAsync(
        string connection,
        Guid streamId,
        int additionalEvents,
        bool reservationHistory
    )
    {
        int head = additionalEvents + 2;
        string name = reservationHistory
            ? "CASE WHEN ordinal % 2 = 1 THEN 'inventory.stock-position.reserved' ELSE 'inventory.stock-position.reservation-released' END"
            : "'inventory.stock-position.received'";
        string payload = reservationHistory
            ? """
                jsonb_build_object(
                    'reservationId', md5('retained-reservation-' || ((ordinal + 1) / 2)::text)::uuid,
                    'operationId', md5('retained-operation-' || ordinal::text)::uuid,
                    'quantity', 1)
                """
            : """'{"quantity":1}'::jsonb""";
        return ExecuteHistorySqlAsync(
            connection,
            $$"""
            INSERT INTO inventory.events (event_id, organization_id, stream_id, stream_version, event_name, schema_version, recorded_at, payload, metadata)
            SELECT gen_random_uuid(), organization_id, id, ordinal + 2, {{name}}, 1,
                updated_at, {{payload}}, '{}'::jsonb
            FROM inventory.event_streams CROSS JOIN generate_series(1, {{additionalEvents}}) AS ordinal
            WHERE id = '{{streamId}}';
            UPDATE inventory.event_streams SET version = {{head}} WHERE id = '{{streamId}}';
            """
        );
    }
}
