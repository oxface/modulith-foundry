using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Sales.Contracts;

public sealed record SalesOrderActivityEntry(
    SalesOrderActivityKind Kind,
    UserId ActorUserId,
    long OrderVersion,
    DateTimeOffset OccurredAt
);
