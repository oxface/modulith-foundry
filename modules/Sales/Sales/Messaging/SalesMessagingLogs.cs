using Microsoft.Extensions.Logging;

namespace ModulithFoundry.Modules.Sales.Messaging;

internal static partial class SalesMessagingLogs
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Sales outbox observation failed ({ErrorType}); last sample is stale, not empty."
    )]
    internal static partial void ObservationFailed(ILogger logger, string errorType);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Sales outbox relay failed ({ErrorType}); committed work remains recoverable."
    )]
    internal static partial void RelayFailed(ILogger logger, string errorType);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Sales outbox message {MessageId} dispatch failed on attempt {Attempt} ({ErrorType}); publication may have succeeded."
    )]
    internal static partial void DispatchFailed(
        ILogger logger,
        Guid messageId,
        int attempt,
        string errorType
    );

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Pending fulfilment dispatch failed; approved work remains recoverable."
    )]
    internal static partial void PendingDispatchFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Fulfilment process {ProcessId} dispatch failed; other pending work will continue."
    )]
    internal static partial void ProcessDispatchFailed(
        ILogger logger,
        Guid processId,
        Exception exception
    );
}
