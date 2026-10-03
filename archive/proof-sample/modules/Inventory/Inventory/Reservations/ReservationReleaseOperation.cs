using System.Text.Json;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Contracts;

namespace ModulithFoundry.Modules.Inventory.Reservations;

// Retained compensation-operation identity, not a second reservation aggregate.
internal sealed class ReservationReleaseOperation : IOrganizationOwned
{
    private ReservationReleaseOperation() => Fingerprint = null!;

    internal Guid OperationId { get; private set; }
    public Guid OrganizationId { get; private set; }
    internal string Fingerprint { get; private set; }
    internal JsonElement Outcome { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }

    internal static ReservationReleaseOperation Complete(
        string fingerprint,
        StockReservationReleaseOutcomeV1 outcome
    ) =>
        new()
        {
            OperationId = outcome.OperationId,
            OrganizationId = outcome.OrganizationId,
            Fingerprint = fingerprint,
            Outcome = JsonSerializer.SerializeToElement(outcome),
            CreatedAt = outcome.CreatedAt,
        };
}
