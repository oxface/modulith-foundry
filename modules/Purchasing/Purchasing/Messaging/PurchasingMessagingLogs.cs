using Microsoft.Extensions.Logging;

namespace ModulithFoundry.Modules.Purchasing.Messaging;

internal static partial class PurchasingMessagingLogs
{
    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "Purchasing Stock Item projection observation failed ({ErrorType}); last readiness sample is stale, not unready."
    )]
    internal static partial void ProjectionObservationFailed(ILogger logger, string errorType);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Purchasing outbox observation failed ({ErrorType}); last sample is stale, not empty."
    )]
    internal static partial void ObservationFailed(ILogger logger, string errorType);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Purchasing outbox relay failed ({ErrorType}); committed work remains recoverable."
    )]
    internal static partial void RelayFailed(ILogger logger, string errorType);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Purchasing outbox message {MessageId} dispatch failed on attempt {Attempts} ({ErrorType}); publication may have succeeded."
    )]
    internal static partial void DispatchFailed(
        ILogger logger,
        Guid messageId,
        int attempts,
        string errorType
    );

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Purchasing pending replenishment scan failed."
    )]
    internal static partial void PendingScanFailed(ILogger logger, Exception exception);
}
