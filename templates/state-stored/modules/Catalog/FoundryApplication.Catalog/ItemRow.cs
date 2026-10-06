namespace ConsumerRoot.Catalog;

internal sealed class ItemRow
{
    public Guid Id { get; set; }
    public required string TenantKey { get; set; }
    public required string Name { get; set; }
}
