using Microsoft.Extensions.Logging;

namespace ModulithFoundry.Modules.Inventory.Messaging;

internal static partial class InventoryMessagingLogs
{
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Inventory outbox relay failed; committed work remains recoverable."
    )]
    internal static partial void RelayFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Inventory outbox message {MessageId} dispatch failed on attempt {Attempt}."
    )]
    internal static partial void DispatchFailed(
        ILogger logger,
        Guid messageId,
        int attempt,
        Exception exception
    );
}
