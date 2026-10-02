using System.Text.Json;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Contracts;

namespace ModulithFoundry.Modules.Inventory.Messaging.Persistence;

internal sealed class InventoryOutboxMessage : IOrganizationOwned
{
    private InventoryOutboxMessage()
    {
        MessageType = null!;
    }

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

    internal static InventoryOutboxMessage Stage(StockReservationOutcomeV1 outcome) =>
        new()
        {
            MessageId = outcome.MessageId,
            OrganizationId = outcome.OrganizationId,
            MessageType = StockReservationOutcomeV1.LogicalName,
            Payload = JsonSerializer.SerializeToElement(outcome),
            CreatedAt = outcome.CreatedAt,
            AvailableAt = outcome.CreatedAt,
        };

    internal static InventoryOutboxMessage Stage(StockItemReferenceChangedV1 changed) =>
        new()
        {
            MessageId = changed.MessageId,
            OrganizationId = changed.Item.OrganizationId,
            MessageType = StockItemReferenceChangedV1.LogicalName,
            Payload = JsonSerializer.SerializeToElement(changed),
            CreatedAt = changed.CreatedAt,
            AvailableAt = changed.CreatedAt,
        };
}
