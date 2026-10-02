using Microsoft.Extensions.Logging;

namespace ModulithFoundry.Modules.Inventory.Messaging;

internal static partial class InventoryMessagingLogs
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Inventory outbox observation failed ({ErrorType}); last sample is stale, not empty."
    )]
    internal static partial void ObservationFailed(ILogger logger, string errorType);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Inventory outbox relay failed ({ErrorType}); committed work remains recoverable."
    )]
    internal static partial void RelayFailed(ILogger logger, string errorType);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Inventory outbox message {MessageId} dispatch failed on attempt {Attempt} ({ErrorType}); publication may have succeeded."
    )]
    internal static partial void DispatchFailed(
        ILogger logger,
        Guid messageId,
        int attempt,
        string errorType
    );
}
