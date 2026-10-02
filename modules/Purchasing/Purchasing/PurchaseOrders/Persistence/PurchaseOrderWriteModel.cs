using System.Text.Json;
using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Purchasing.PurchaseOrders.Persistence;

internal sealed class PurchaseOrderWriteModel : IOrganizationOwned
{
    private PurchaseOrderWriteModel() { }

    internal Guid StreamId { get; private set; }
    public Guid OrganizationId { get; private set; }
    internal string Code { get; private set; } = null!;
    internal long Version { get; private set; }
    internal JsonElement State { get; private set; }

    internal static PurchaseOrderWriteModel Create(Guid id, Guid organizationId) =>
        new() { StreamId = id, OrganizationId = organizationId };

    internal PurchaseOrderState ToState()
    {
        try
        {
            return State.Deserialize<PurchaseOrderState>(PurchaseOrderEventSerializer.Options)
                ?? throw new JsonException();
        }
        catch (JsonException exception)
        {
            throw new PurchaseOrderIntegrityException(
                StreamId,
                PurchaseOrderIntegrityFailure.InvalidWriteModel,
                exception
            );
        }
    }

    internal void Apply(PurchaseOrderState state, long version)
    {
        Code = state.Code;
        Version = version;
        State = JsonSerializer.SerializeToElement(state, PurchaseOrderEventSerializer.Options);
    }
}
