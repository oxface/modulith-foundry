using System.Text;
using RabbitMQ.Client;
using Rootbolt.Messaging;

namespace ModulithFoundry.Samples.Wholesale.EventPersistenceDemo;

/// <summary>Consumer-owned native RabbitMQ acceptance and route policy; no transport code lives in Rootbolt.</summary>
public sealed class InventoryRabbitMqPublisher(IChannel channel) : IMessagePublisher
{
    public async Task PublishAsync(OutgoingMessage message, CancellationToken cancellationToken)
    {
        if (
            message.RouteKey != "inventory.stock-issues"
            || message.MessageName
                is not ("inventory.stock-issue-recorded" or "inventory.stock-issue-declined")
            || message.SchemaVersion != 1
            || message.TenantKey is null
        )
            throw new InvalidDataException(
                "This publisher accepts tenant-scoped stock issue recorded/declined v1 only."
            );
        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = message.MessageId.ToString(),
            Type = message.MessageName,
            // Preserve the producer's conversation identity across native publication/retries.
            CorrelationId = message.CorrelationId,
            Headers = new Dictionary<string, object?>
            {
                ["schema-version"] = message.SchemaVersion,
                ["owner-key"] = message.TenantKey,
                ["producer-module"] = "inventory",
            },
        };
        if (message.CausationId is not null)
            properties.Headers["causation-id"] = message.CausationId;
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(TimeSpan.FromSeconds(5));
        // The owning host enables confirmation tracking; native BasicPublishAsync then awaits acceptance
        // and throws for a nack or mandatory unroutable return. A successful socket write alone is insufficient.
        await channel.BasicPublishAsync(
            "",
            message.RouteKey,
            mandatory: true,
            properties,
            Encoding.UTF8.GetBytes(message.Payload.GetRawText()),
            bounded.Token
        );
    }
}
