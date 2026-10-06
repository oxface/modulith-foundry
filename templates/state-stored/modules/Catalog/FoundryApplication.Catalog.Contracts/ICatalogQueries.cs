namespace ConsumerRoot.Catalog.Contracts;

// Catalog owns tenant-scoped item names. This is editable consumer business vocabulary.
public sealed record CatalogItem(Guid Id, string Name);

public interface ICatalogQueries
{
    global::System.Threading.Tasks.Task<IReadOnlyList<CatalogItem>> ListAsync(
        CancellationToken cancellationToken
    );
}
