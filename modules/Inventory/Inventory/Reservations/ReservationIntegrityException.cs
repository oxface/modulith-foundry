namespace ModulithFoundry.Modules.Inventory.Reservations;

internal sealed class ReservationIntegrityException(
    Guid reservationOperationId,
    ReservationIntegrityFailure failure,
    Exception? innerException = null
)
    : Exception(
        $"Reservation operation '{reservationOperationId}' failed integrity validation: {failure}.",
        innerException
    )
{
    internal Guid ReservationOperationId { get; } = reservationOperationId;
    internal ReservationIntegrityFailure Failure { get; } = failure;
}

internal enum ReservationIntegrityFailure
{
    StoredOutcomeUnreadable = 1,
    WriteModelMissing = 2,
    ReservationStateMismatch = 3,
}
