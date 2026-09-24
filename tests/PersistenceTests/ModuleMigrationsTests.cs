using System.Diagnostics;
using System.Text;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ModulithFoundry.PersistenceTests;

public sealed class ModuleMigrationsTests
{
    private static readonly TimeSpan MigratorTimeout = TimeSpan.FromMinutes(2);

    [Fact]
    public async Task Migrator_ExecutedTwiceAgainstSameDatabase_IsIdempotent()
    {
        await using PostgreSqlContainer postgres = CreatePostgresContainer();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        string connectionString = WithSingleConnectionPool(postgres.GetConnectionString());

        ProcessResult firstRun = await RunMigratorAsync(connectionString);
        AssertMigratorSucceeded(firstRun);
        IReadOnlyList<(string Schema, long Count)> firstCounts =
            await ReadMigrationHistoryCountsAsync(connectionString);

        ProcessResult secondRun = await RunMigratorAsync(connectionString);
        AssertMigratorSucceeded(secondRun);
        IReadOnlyList<(string Schema, long Count)> secondCounts =
            await ReadMigrationHistoryCountsAsync(connectionString);

        Assert.Equal(
            ["access", "inventory", "purchasing", "sales"],
            firstCounts.Select(history => history.Schema));
        Assert.All(firstCounts, history => Assert.True(history.Count > 0));
        Assert.Equal(firstCounts, secondCounts);
    }

    [Fact]
    public async Task Migrator_AdvisoryLockHeld_WaitsWithoutApplyingMigrations()
    {
        await using PostgreSqlContainer postgres = CreatePostgresContainer();
        await postgres.StartAsync(TestContext.Current.CancellationToken);
        string connectionString = WithSingleConnectionPool(postgres.GetConnectionString());
        string inspectionConnectionString = WithPoolingDisabled(connectionString);
        await using var lockConnection = new NpgsqlConnection(inspectionConnectionString);
        await lockConnection.OpenAsync(TestContext.Current.CancellationToken);
        await SetMigrationLockAsync(lockConnection, acquire: true);

        using Process process = StartMigrator(connectionString);
        Task<string> standardError = process.StandardError.ReadToEndAsync(
            TestContext.Current.CancellationToken);

        try
        {
            try
            {
                string outputBeforeRelease = await ReadUntilAsync(
                    process.StandardOutput,
                    "Waiting for the PostgreSQL migration advisory lock",
                    MigratorTimeout,
                    TestContext.Current.CancellationToken);

                Assert.Contains(
                    "Waiting for the PostgreSQL migration advisory lock",
                    outputBeforeRelease,
                    StringComparison.Ordinal);
                Assert.True(await WaitForBlockedAdvisoryLockAsync(
                    inspectionConnectionString,
                    MigratorTimeout));
                Assert.Empty(await ReadMigrationHistorySchemasAsync(inspectionConnectionString));
            }
            finally
            {
                await SetMigrationLockAsync(lockConnection, acquire: false);
            }

            await WaitForExitWithinTimeoutAsync(process);
            string remainingOutput = await process.StandardOutput.ReadToEndAsync(
                TestContext.Current.CancellationToken);
            ProcessResult result = new(process.ExitCode, remainingOutput, await standardError);
            AssertMigratorSucceeded(result);
            Assert.Equal(
                ["access", "inventory", "purchasing", "sales"],
                await ReadMigrationHistorySchemasAsync(inspectionConnectionString));
        }
        finally
        {
            await TerminateIfRunningAsync(process);
        }
    }

    private static PostgreSqlContainer CreatePostgresContainer() =>
        new PostgreSqlBuilder("postgres:18.6").Build();

    private static string WithSingleConnectionPool(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            MaxPoolSize = 1,
        };
        return builder.ConnectionString;
    }

    private static string WithPoolingDisabled(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Pooling = false,
        };
        return builder.ConnectionString;
    }

    private static Process StartMigrator(string connectionString)
    {
        string migratorPath = Path.Combine(AppContext.BaseDirectory, "ModulithFoundry.Migrator.dll");
        Assert.True(File.Exists(migratorPath), $"Migrator executable was not found at '{migratorPath}'.");

        var startInfo = new ProcessStartInfo("dotnet", migratorPath)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        startInfo.Environment["ConnectionStrings__database"] = connectionString;

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the Migrator process.");
    }

    private static async Task<ProcessResult> RunMigratorAsync(string connectionString)
    {
        using Process process = StartMigrator(connectionString);
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(
            TestContext.Current.CancellationToken);
        Task<string> standardError = process.StandardError.ReadToEndAsync(
            TestContext.Current.CancellationToken);

        try
        {
            await WaitForExitWithinTimeoutAsync(process);
            return new ProcessResult(process.ExitCode, await standardOutput, await standardError);
        }
        finally
        {
            await TerminateIfRunningAsync(process);
        }
    }

    private static async Task WaitForExitWithinTimeoutAsync(Process process)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        deadline.CancelAfter(MigratorTimeout);

        try
        {
            await process.WaitForExitAsync(deadline.Token);
        }
        catch (OperationCanceledException) when (!TestContext.Current.CancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Migrator did not exit within {MigratorTimeout}.");
        }
    }

    private static async Task TerminateIfRunningAsync(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
        }
    }

    private static void AssertMigratorSucceeded(ProcessResult result) =>
        Assert.True(
            result.ExitCode == 0,
            $"Migrator exited with {result.ExitCode}. stdout: {result.StandardOutput} stderr: {result.StandardError}");

    private static async Task<string> ReadUntilAsync(
        StreamReader reader,
        string expected,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        var output = new StringBuilder();

        while (true)
        {
            string? line = await reader.ReadLineAsync(deadline.Token);
            if (line is null)
            {
                break;
            }

            output.AppendLine(line);
            if (line.Contains(expected, StringComparison.Ordinal))
            {
                return output.ToString();
            }
        }

        Assert.Fail($"Migrator output did not contain '{expected}'. Output: {output}");
        return output.ToString();
    }

    private static async Task<bool> WaitForBlockedAdvisoryLockAsync(
        string connectionString,
        TimeSpan timeout)
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < timeout)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            const string sql = """
                SELECT EXISTS (
                    SELECT 1
                    FROM pg_locks
                    WHERE locktype = 'advisory' AND NOT granted);
                """;
            await using var command = new NpgsqlCommand(sql, connection);
            if ((bool)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken)
                ?? false))
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        }

        return false;
    }

    private static async Task SetMigrationLockAsync(NpgsqlConnection connection, bool acquire)
    {
        const string acquireSql =
            "SELECT pg_advisory_lock(hashtextextended('modulith-foundry:migrations', 0));";
        const string releaseSql =
            "SELECT pg_advisory_unlock(hashtextextended('modulith-foundry:migrations', 0));";
        await using var command = new NpgsqlCommand(acquire ? acquireSql : releaseSql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<IReadOnlyList<(string Schema, long Count)>>
        ReadMigrationHistoryCountsAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        const string sql = """
            SELECT table_schema,
                   (xpath('/row/count/text()', query_to_xml(
                       format('SELECT COUNT(*) AS count FROM %I.%I', table_schema, table_name),
                       false,
                       true,
                       '')))[1]::text::bigint
            FROM information_schema.tables
            WHERE table_name = '__EFMigrationsHistory'
            ORDER BY table_schema;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(
            TestContext.Current.CancellationToken);
        var histories = new List<(string Schema, long Count)>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            histories.Add((reader.GetString(0), reader.GetInt64(1)));
        }

        return histories;
    }

    private static async Task<IReadOnlyList<string>> ReadMigrationHistorySchemasAsync(
        string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        const string sql = """
            SELECT table_schema
            FROM information_schema.tables
            WHERE table_name = '__EFMigrationsHistory'
            ORDER BY table_schema;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(
            TestContext.Current.CancellationToken);
        var schemas = new List<string>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            schemas.Add(reader.GetString(0));
        }

        return schemas;
    }

    private sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
