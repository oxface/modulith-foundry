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
            || message.MessageName != "inventory.stock-issue-recorded"
            || message.SchemaVersion != 1
            || message.TenantKey is null
        )
            throw new InvalidDataException(
                "This publisher accepts tenant-scoped inventory.stock-issue-recorded v1 only."
            );
        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = message.MessageId.ToString(),
            Type = message.MessageName,
            // Sample policy: group notifications by stock position. Correlation is not a deduplication identity.
            CorrelationId = message.Payload.GetProperty("stockPositionId").GetGuid().ToString(),
            Headers = new Dictionary<string, object?>
            {
                ["schema-version"] = message.SchemaVersion,
                ["owner-key"] = message.TenantKey,
            },
        };
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
