using System.Text.Json;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Contracts;

namespace ModulithFoundry.Modules.Inventory.Reservations;

// Durable semantic-idempotency receipt, not a second business aggregate.
internal sealed class ReservationOperation : IOrganizationOwned
{
    private ReservationOperation()
    {
        Fingerprint = null!;
    }

    internal Guid OperationId { get; private set; }
    public Guid OrganizationId { get; private set; }
    internal string Fingerprint { get; private set; }
    internal JsonElement Outcome { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }

    internal static ReservationOperation Complete(
        string fingerprint,
        StockReservationOutcomeV1 outcome
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
