using System.Text.Json;

namespace ModulithFoundry.Modules.Inventory.StockPositions.Persistence;

internal sealed record StockPositionEventMetadata(
    Guid OrganizationId,
    Guid ActorUserId,
    string? CorrelationId,
    string? CausationId,
    string? TraceId)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    internal JsonElement ToJson() => JsonSerializer.SerializeToElement(this, SerializerOptions);
}
