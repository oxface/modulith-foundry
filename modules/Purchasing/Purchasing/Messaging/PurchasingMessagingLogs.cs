using Microsoft.Extensions.Logging;

namespace ModulithFoundry.Modules.Purchasing.Messaging;

internal static partial class PurchasingMessagingLogs
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Purchasing outbox relay failed."
    )]
    internal static partial void RelayFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Purchasing outbox message {MessageId} failed on attempt {Attempts}."
    )]
    internal static partial void DispatchFailed(
        ILogger logger,
        Guid messageId,
        int attempts,
        Exception exception
    );

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Purchasing pending replenishment scan failed."
    )]
    internal static partial void PendingScanFailed(ILogger logger, Exception exception);
}
