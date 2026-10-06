using System.Text.Json;
using ModulithFoundry.Events.Serialization;

namespace ModulithFoundry.Samples.Wholesale.EventCodecDemo;

// The caller owns parsing and envelope extraction; the codec sees only identity and payload.
internal static class FixtureEvents
{
    internal static IEnumerable<TEvent> Read<TEvent>(
        JsonEventCodec<TEvent> codec,
        params string[] paths
    )
        where TEvent : class
    {
        foreach (string path in paths)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement envelope = document.RootElement;
            yield return codec.Deserialize(
                envelope.GetProperty("eventName").GetString()!,
                envelope.GetProperty("schemaVersion").GetInt32(),
                envelope.GetProperty("payload")
            );
        }
    }
}
