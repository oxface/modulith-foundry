using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Inventory.Messaging.Persistence;
using ModulithFoundry.Modules.Inventory.Persistence;
using Rebus.Bus;
using Rebus.Messages;

namespace ModulithFoundry.Modules.Inventory.Messaging;

internal sealed class InventoryOutboxRelay(
    IServiceScopeFactory scopes,
    IBus bus,
    ILogger<InventoryOutboxRelay> logger
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
                InventoryMessagingLogs.RelayFailed(logger, exception);
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
        InventoryDbContext context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Guid lease = Guid.NewGuid();
        List<InventoryOutboxMessage> claimed = await context
            .OutboxMessages.FromSqlInterpolated(
                $$"""
                WITH candidate AS (
                    SELECT message_id FROM inventory.outbox_messages
                    WHERE dispatched_at IS NULL AND available_at <= clock_timestamp()
                        AND (lease_until IS NULL OR lease_until <= clock_timestamp())
                    ORDER BY created_at, message_id FOR UPDATE SKIP LOCKED LIMIT 1
                )
                UPDATE inventory.outbox_messages message
                SET lease_token = {{lease}}, lease_until = clock_timestamp() + interval '30 seconds', attempts = attempts + 1
                FROM candidate WHERE message.message_id = candidate.message_id
                RETURNING message.*
                """
            )
            .IgnoreQueryFilters([InventoryDbContext.OrganizationScopeFilter])
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        if (claimed.Count == 0)
            return false;
        InventoryOutboxMessage message = claimed[0];
        try
        {
            await PublishAsync(message);
            await context.Database.ExecuteSqlInterpolatedAsync(
                $$"""
                UPDATE inventory.outbox_messages
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
            InventoryMessagingLogs.DispatchFailed(
                logger,
                message.MessageId,
                message.Attempts,
                exception
            );
            int delay = 1 << Math.Min(message.Attempts, 6);
            await context.Database.ExecuteSqlInterpolatedAsync(
                $$"""
                UPDATE inventory.outbox_messages
                SET available_at = clock_timestamp() + {{delay}} * interval '1 second', lease_token = NULL, lease_until = NULL
                WHERE message_id = {{message.MessageId}} AND lease_token = {{lease}} AND dispatched_at IS NULL
                """,
                cancellationToken
            );
        }
        return true;
    }

    private Task PublishAsync(InventoryOutboxMessage message)
    {
        var headers = new Dictionary<string, string>
        {
            [Headers.MessageId] = message.MessageId.ToString(),
            ["producer-module"] = "inventory",
        };
        switch (message.MessageType)
        {
            case StockReservationReleaseOutcomeV1.LogicalName:
                var release =
                    message.Payload.Deserialize<StockReservationReleaseOutcomeV1>()
                    ?? throw new InvalidDataException("Inventory release payload is unreadable.");
                headers[Headers.CorrelationId] = release.ProcessId.ToString();
                headers["causation-id"] = release.CausationId.ToString();
                return bus.Advanced.Topics.Publish(message.MessageType, release, headers);
            case StockReservationOutcomeV1.LogicalName:
                StockReservationOutcomeV1 outcome =
                    message.Payload.Deserialize<StockReservationOutcomeV1>()
                    ?? throw new InvalidDataException("Inventory outcome payload is unreadable.");
                headers[Headers.CorrelationId] = outcome.ProcessId.ToString();
                headers["causation-id"] = outcome.CausationId.ToString();
                return bus.Advanced.Topics.Publish(message.MessageType, outcome, headers);
            case StockItemReferenceChangedV1.LogicalName:
                StockItemReferenceChangedV1 changed =
                    message.Payload.Deserialize<StockItemReferenceChangedV1>()
                    ?? throw new InvalidDataException("Inventory reference payload is unreadable.");
                headers[Headers.CorrelationId] = changed.Item.StockItemId.ToString();
                return bus.Advanced.Topics.Publish(message.MessageType, changed, headers);
            default:
                throw new InvalidDataException("Inventory outbox message type is unsupported.");
        }
    }
}
