using System.Text.Json;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Contracts;

namespace ModulithFoundry.Modules.Sales.Messaging.Persistence;

internal sealed class SalesOutboxMessage : IOrganizationOwned
{
    private SalesOutboxMessage()
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

    internal static SalesOutboxMessage Stage(ReserveStockV1 command) =>
        new()
        {
            MessageId = command.MessageId,
            OrganizationId = command.OrganizationId,
            MessageType = ReserveStockV1.LogicalName,
            Payload = JsonSerializer.SerializeToElement(command),
            CreatedAt = command.CreatedAt,
            AvailableAt = command.CreatedAt,
        };
}
