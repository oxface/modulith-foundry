using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Samples.Wholesale.Purchasing.PurchaseOrders;
using Npgsql;

namespace ModulithFoundry.Samples.Wholesale.Purchasing;

// Native composition policy: only these module stream constraints are version conflicts.
public static class PurchasingAppendFailures
{
    public static bool IsVersionConflict(DbUpdateException failure) =>
        failure is DbUpdateConcurrencyException { Entries.Count: > 0 } concurrency
            && concurrency.Entries.All(entry =>
                entry.Entity is EventStream or PurchaseOrderStateRow
            )
        || failure.InnerException
            is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                SchemaName: "purchasing",
                ConstraintName: "PK_event_streams"
                    or "IX_events_organization_key_stream_id_stream_version"
            };
}
