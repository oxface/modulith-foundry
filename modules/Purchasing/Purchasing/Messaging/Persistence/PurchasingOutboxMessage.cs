using System.Text.Json;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Purchasing.Contracts;

namespace ModulithFoundry.Modules.Purchasing.Messaging.Persistence;

internal sealed class PurchasingOutboxMessage : IOrganizationOwned
{
    private PurchasingOutboxMessage() => MessageType = null!;

    internal Guid MessageId { get; private set; }
    public Guid OrganizationId { get; private set; }
    internal string MessageType { get; private set; }
    internal JsonElement Payload { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset AvailableAt { get; private set; }
    internal DateTimeOffset? DispatchedAt { get; private set; }
    internal Guid? LeaseToken { get; private set; }
    internal DateTimeOffset? LeaseUntil { get; private set; }
    internal int Attempts { get; private set; }

    internal static PurchasingOutboxMessage Stage(ReplenishmentRequirementCreatedV1 message) =>
        new()
        {
            MessageId = message.MessageId,
            OrganizationId = message.OrganizationId,
            MessageType = ReplenishmentRequirementCreatedV1.LogicalName,
            Payload = JsonSerializer.SerializeToElement(message),
            CreatedAt = message.CreatedAt,
            AvailableAt = message.CreatedAt,
        };
}
