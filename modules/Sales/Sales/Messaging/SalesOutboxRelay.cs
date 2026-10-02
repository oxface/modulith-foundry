using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Purchasing.Contracts;
using ModulithFoundry.Modules.Sales.Messaging.Persistence;
using ModulithFoundry.Modules.Sales.Persistence;
using Rebus.Bus;
using Rebus.Messages;

namespace ModulithFoundry.Modules.Sales.Messaging;

internal sealed class SalesOutboxRelay(
    IServiceScopeFactory scopes,
    IBus bus,
    ILogger<SalesOutboxRelay> logger
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
                SalesMessagingLogs.RelayFailed(logger, exception);
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
        SalesDbContext context = scope.ServiceProvider.GetRequiredService<SalesDbContext>();
        Guid lease = Guid.NewGuid();
        List<SalesOutboxMessage> claimed = await context
            .OutboxMessages.FromSqlInterpolated(
                $$"""
                WITH candidate AS (
                    SELECT message_id FROM sales.outbox_messages
                    WHERE dispatched_at IS NULL AND available_at <= clock_timestamp()
                        AND (lease_until IS NULL OR lease_until <= clock_timestamp())
                    ORDER BY created_at, message_id FOR UPDATE SKIP LOCKED LIMIT 1
                )
                UPDATE sales.outbox_messages message
                SET lease_token = {{lease}}, lease_until = clock_timestamp() + interval '30 seconds', attempts = attempts + 1
                FROM candidate WHERE message.message_id = candidate.message_id
                RETURNING message.*
                """
            )
            .IgnoreQueryFilters([SalesDbContext.OrganizationScopeFilter])
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        if (claimed.Count == 0)
            return false;
        SalesOutboxMessage message = claimed[0];
        try
        {
            object command;
            Guid processId;
            switch (message.MessageType)
            {
                case ReserveStockV1.LogicalName:
                    var reservation =
                        message.Payload.Deserialize<ReserveStockV1>()
                        ?? throw new InvalidDataException(
                            "Sales reservation payload is unreadable."
                        );
                    command = reservation;
                    processId = reservation.ProcessId;
                    break;
                case CreateReplenishmentRequirementV1.LogicalName:
                    var replenishment =
                        message.Payload.Deserialize<CreateReplenishmentRequirementV1>()
                        ?? throw new InvalidDataException(
                            "Sales replenishment payload is unreadable."
                        );
                    command = replenishment;
                    processId = replenishment.ProcessId;
                    break;
                default:
                    throw new InvalidDataException("Sales outbox message type is unsupported.");
            }
            await bus.Send(
                command,
                new Dictionary<string, string>
                {
                    [Headers.MessageId] = message.MessageId.ToString(),
                    [Headers.CorrelationId] = processId.ToString(),
                    ["causation-id"] = processId.ToString(),
                    ["producer-module"] = "sales",
                }
            );
            await context.Database.ExecuteSqlInterpolatedAsync(
                $$"""
                UPDATE sales.outbox_messages
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
            SalesMessagingLogs.DispatchFailed(
                logger,
                message.MessageId,
                message.Attempts,
                exception
            );
            int delay = 1 << Math.Min(message.Attempts, 6);
            await context.Database.ExecuteSqlInterpolatedAsync(
                $$"""
                UPDATE sales.outbox_messages
                SET available_at = clock_timestamp() + {{delay}} * interval '1 second', lease_token = NULL, lease_until = NULL
                WHERE message_id = {{message.MessageId}} AND lease_token = {{lease}} AND dispatched_at IS NULL
                """,
                cancellationToken
            );
        }
        return true;
    }
}
