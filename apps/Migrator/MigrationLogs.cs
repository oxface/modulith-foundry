using Microsoft.Extensions.Logging;

namespace ModulithFoundry.Migrator;

internal static partial class MigrationLogs
{
    [LoggerMessage(1, LogLevel.Information, "Waiting for the PostgreSQL migration advisory lock")]
    public static partial void WaitingForLock(ILogger logger);

    [LoggerMessage(2, LogLevel.Information, "Acquired the PostgreSQL migration advisory lock")]
    public static partial void LockAcquired(ILogger logger);

    [LoggerMessage(3, LogLevel.Information, "Applying {ModuleName} module migrations")]
    public static partial void ApplyingModule(ILogger logger, string moduleName);

    [LoggerMessage(4, LogLevel.Information, "Released the PostgreSQL migration advisory lock")]
    public static partial void LockReleased(ILogger logger);

    [LoggerMessage(5, LogLevel.Information, "All module migrations completed successfully")]
    public static partial void MigrationsCompleted(ILogger logger);

    [LoggerMessage(6, LogLevel.Critical, "Module migration failed")]
    public static partial void MigrationFailed(ILogger logger, Exception exception);
}
