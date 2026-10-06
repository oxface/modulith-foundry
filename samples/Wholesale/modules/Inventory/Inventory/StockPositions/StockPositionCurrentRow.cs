using System.Text.Json;

namespace ModulithFoundry.Samples.Wholesale.Inventory.StockPositions;

internal sealed class StockPositionCurrentRow
{
    private static readonly JsonSerializerOptions StateJson = new(JsonSerializerDefaults.Web)
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    };

    public string OrganizationKey { get; set; } = null!;
    public Guid StreamId { get; set; }
    public long Version { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public JsonElement State { get; set; }

    internal StockPositionState ReadState()
    {
        var state =
            State.Deserialize<StockPositionState>(StateJson)
            ?? throw new InvalidDataException("The stock-position write view is unreadable.");
        if (
            state.StockItemId == Guid.Empty
            || state.StockingLocationId == Guid.Empty
            || string.IsNullOrWhiteSpace(state.BaseUnitCode)
            || state.OnHand < 0
        )
            throw new InvalidDataException("The stock-position write view has invalid state.");
        return state;
    }

    internal static StockPositionCurrentRow Prepare(
        string owner,
        Guid id,
        long version,
        DateTimeOffset recordedAt,
        StockPositionState state
    ) =>
        new()
        {
            OrganizationKey = owner,
            StreamId = id,
            Version = version,
            RecordedAt = recordedAt,
            State = JsonSerializer.SerializeToElement(state, StateJson),
        };
}
