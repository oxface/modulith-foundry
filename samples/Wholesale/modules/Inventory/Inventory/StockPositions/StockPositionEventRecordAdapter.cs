using ModulithFoundry.Events.Serialization;
using ModulithFoundry.EventSourcing.EntityFrameworkCore;

namespace ModulithFoundry.Samples.Wholesale.Inventory.StockPositions;

internal sealed class StockPositionEventRecordAdapter
    : EventRecordAdapter<IStockPositionEvent, EventStream, StoredEvent>
{
    private readonly JsonEventCodec<IStockPositionEvent> codec = StockPositionCodec.CreateCodec();
    public override string StreamType => StockPositionHistoryReader.StreamType;

    public override StoredEvent CreateRecord(IStockPositionEvent @event, EventStream stream)
    {
        var encoded = codec.Serialize(@event);
        return new StoredEvent
        {
            OrganizationKey = stream.OrganizationKey,
            EventName = encoded.EventName,
            SchemaVersion = encoded.SchemaVersion,
            Payload = encoded.Payload,
        };
    }
}
