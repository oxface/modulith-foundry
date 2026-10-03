namespace ModulithFoundry.Modules.Inventory.StockPositions.Events;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class StoredEventTypeAttribute(string name, int schemaVersion) : Attribute
{
    internal string Name { get; } = name;

    internal int SchemaVersion { get; } = schemaVersion;
}
