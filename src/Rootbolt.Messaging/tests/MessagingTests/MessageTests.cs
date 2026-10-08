using System.Text.Json;

namespace Rootbolt.Messaging.Tests;

public sealed class MessageTests
{
    [Fact]
    public void TypedConstructionUsesConsumerJsonPolicyAndCapturesThePayloadBeforeLaterChanges()
    {
        var payload = new List<WireItem> { new("first") };
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Guid id = Guid.NewGuid();
        var message = OutgoingMessage.FromPayload(
            id,
            "commands.export",
            "exports.render",
            2,
            payload,
            options,
            "Tenant A"
        );
        payload.Add(new("later"));

        Assert.Equal(id, message.MessageId);
        Assert.Equal("commands.export", message.RouteKey);
        Assert.Equal("exports.render", message.MessageName);
        Assert.Equal("Tenant A", message.TenantKey);
        var item = Assert.Single(message.Payload.EnumerateArray());
        Assert.Equal("first", item.GetProperty("displayName").GetString());
        Assert.False(item.TryGetProperty("DisplayName", out _));
    }

    [Fact]
    public void TypedConstructionRequiresExplicitJsonPolicyAndANonNullSerializedPayload()
    {
        Assert.Throws<ArgumentNullException>(() =>
            OutgoingMessage.FromPayload(Guid.NewGuid(), "d", "m", 1, new WireItem("x"), null!)
        );
        Assert.Throws<ArgumentException>(() =>
            OutgoingMessage.FromPayload<WireItem?>(Guid.NewGuid(), "d", "m", 1, null, new())
        );
    }

    [Fact]
    public void RetainedPayloadOutlivesItsSourceAndPreservesConsumerMetadata()
    {
        Guid id = Guid.NewGuid();
        OutgoingMessage message;
        using (var document = JsonDocument.Parse("{\"optional\":null,\"quantity\":2.5}"))
            message = new(
                id,
                "commands.export",
                "exports.render",
                3,
                document.RootElement,
                "Tenant A"
            );
        Assert.Equal(id, message.MessageId);
        Assert.Equal("commands.export", message.RouteKey);
        Assert.Equal("exports.render", message.MessageName);
        Assert.Equal(3, message.SchemaVersion);
        Assert.Equal("Tenant A", message.TenantKey);
        Assert.Equal(2.5m, message.Payload.GetProperty("quantity").GetDecimal());
        Assert.Equal(JsonValueKind.Null, message.Payload.GetProperty("optional").ValueKind);
    }

    [Fact]
    public void UndefinedOrNullPayloadCannotBecomeDurableWork()
    {
        Assert.Throws<ArgumentException>(() =>
            new OutgoingMessage(Guid.NewGuid(), "d", "m", 1, default)
        );
        Assert.Throws<ArgumentException>(() =>
            new OutgoingMessage(
                Guid.NewGuid(),
                "d",
                "m",
                1,
                JsonSerializer.SerializeToElement<object?>(null)
            )
        );
    }

    [Fact]
    public void InvalidIdentityOrSchemaFailsAtTheEnvelopeBoundary()
    {
        var payload = JsonSerializer.SerializeToElement(new { value = 1 });
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new OutgoingMessage(Guid.Empty, "d", "m", 1, payload)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new OutgoingMessage(Guid.NewGuid(), "d", "m", 0, payload)
        );
        Assert.Throws<ArgumentException>(() =>
            new OutgoingMessage(Guid.NewGuid(), " ", "m", 1, payload)
        );
        Assert.Throws<ArgumentException>(() =>
            new OutgoingMessage(Guid.NewGuid(), "d", "m", 1, payload, " ")
        );
    }

    private sealed record WireItem(string DisplayName);
}
