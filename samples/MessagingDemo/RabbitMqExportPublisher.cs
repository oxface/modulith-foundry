using System.Text;
using RabbitMQ.Client;
using Rootbolt.Messaging;

namespace ModulithFoundry.Samples.MessagingDemo;

// Logical routing is consumer policy; the physical queue is explicitly configured.
public sealed class RabbitMqExportPublisher(IChannel channel, string queue) : IMessagePublisher
{
    public async Task PublishAsync(OutgoingMessage message, CancellationToken cancellationToken)
    {
        if (
            message.RouteKey != "exports.render"
            || message.MessageName != "exports.render"
            || message.SchemaVersion != 1
        )
            throw new InvalidDataException("Only exports.render v1 is routed by this publisher.");
        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = message.MessageId.ToString(),
            Type = message.MessageName,
            CorrelationId = message.CorrelationId,
            Headers = new Dictionary<string, object?>
            {
                ["producer-module"] = "exports",
                ["schema-version"] = message.SchemaVersion,
            },
        };
        if (message.TenantKey is not null)
            properties.Headers["tenant-key"] = message.TenantKey;
        if (message.CausationId is not null)
            properties.Headers["causation-id"] = message.CausationId;
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(TimeSpan.FromSeconds(5));
        await channel.BasicPublishAsync(
            "",
            queue,
            mandatory: true,
            properties,
            Encoding.UTF8.GetBytes(message.Payload.GetRawText()),
            bounded.Token
        );
    }
}
