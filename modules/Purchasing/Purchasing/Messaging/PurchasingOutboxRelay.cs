using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModulithFoundry.Modules.Purchasing.Contracts;
using ModulithFoundry.Modules.Purchasing.Messaging.Persistence;
using ModulithFoundry.Modules.Purchasing.Persistence;
using Rebus.Bus;
using Rebus.Messages;

namespace ModulithFoundry.Modules.Purchasing.Messaging;

internal sealed class PurchasingOutboxRelay(
    IServiceScopeFactory scopes,
    IBus bus,
    ILogger<PurchasingOutboxRelay> logger,
    PurchasingMessagingMetrics metrics
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await DispatchNextAsync(stoppingToken))
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                PurchasingMessagingLogs.RelayFailed(logger, exception.GetType().Name);
                metrics.RelayFailed();
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    private async Task<bool> DispatchNextAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        PurchasingDbContext context =
            scope.ServiceProvider.GetRequiredService<PurchasingDbContext>();
        Guid lease = Guid.NewGuid();
        List<PurchasingOutboxMessage> claimed = await context
            .OutboxMessages.FromSqlInterpolated(
                $$"""
                WITH candidate AS (
                    SELECT message_id FROM purchasing.outbox_messages
                    WHERE dispatched_at IS NULL AND available_at <= clock_timestamp()
                        AND (lease_until IS NULL OR lease_until <= clock_timestamp())
                    ORDER BY created_at, message_id FOR UPDATE SKIP LOCKED LIMIT 1
                )
                UPDATE purchasing.outbox_messages message
                SET lease_token = {{lease}}, lease_until = clock_timestamp() + interval '30 seconds', attempts = attempts + 1
                FROM candidate WHERE message.message_id = candidate.message_id
                RETURNING message.*
                """
            )
            .IgnoreQueryFilters([PurchasingDbContext.OrganizationScopeFilter])
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        if (claimed.Count == 0)
            return false;
        PurchasingOutboxMessage message = claimed[0];
        try
        {
            object notification;
            Guid processId;
            Guid causationId;
            switch (message.MessageType)
            {
                case ReplenishmentRequirementCreatedV1.LogicalName:
                    var created =
                        message.Payload.Deserialize<ReplenishmentRequirementCreatedV1>()
                        ?? throw new InvalidDataException(
                            "Purchasing created payload is unreadable."
                        );
                    notification = created;
                    processId = created.ProcessId;
                    causationId = created.CausationId;
                    break;
                case ReplenishmentRequestRejectedV1.LogicalName:
                    var rejected =
                        message.Payload.Deserialize<ReplenishmentRequestRejectedV1>()
                        ?? throw new InvalidDataException(
                            "Purchasing rejected payload is unreadable."
                        );
                    notification = rejected;
                    processId = rejected.ProcessId;
                    causationId = rejected.CausationId;
                    break;
                default:
                    throw new InvalidDataException(
                        "Purchasing outbox message type is unsupported."
                    );
            }
            await bus.Advanced.Topics.Publish(
                message.MessageType,
                notification,
                new Dictionary<string, string>
                {
                    [Headers.MessageId] = message.MessageId.ToString(),
                    [Headers.CorrelationId] = processId.ToString(),
                    ["causation-id"] = causationId.ToString(),
                    ["producer-module"] = "purchasing",
                }
            );
            await context.Database.ExecuteSqlInterpolatedAsync(
                $$"""
                UPDATE purchasing.outbox_messages
                SET dispatched_at = clock_timestamp(), lease_token = NULL, lease_until = NULL
                WHERE message_id = {{message.MessageId}} AND lease_token = {{lease}} AND dispatched_at IS NULL
                """,
                cancellationToken
            );
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            PurchasingMessagingLogs.DispatchFailed(
                logger,
                message.MessageId,
                message.Attempts,
                exception.GetType().Name
            );
            metrics.DispatchFailed();
            int delay = 1 << Math.Min(message.Attempts, 6);
            await context.Database.ExecuteSqlInterpolatedAsync(
                $$"""
                UPDATE purchasing.outbox_messages
                SET available_at = clock_timestamp() + {{delay}} * interval '1 second', lease_token = NULL, lease_until = NULL
                WHERE message_id = {{message.MessageId}} AND lease_token = {{lease}} AND dispatched_at IS NULL
                """,
                cancellationToken
            );
        }
        return true;
    }
}
