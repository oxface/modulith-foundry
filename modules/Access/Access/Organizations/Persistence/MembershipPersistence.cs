using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using Npgsql;

namespace ModulithFoundry.Modules.Access.Organizations.Persistence;

internal static class MembershipPersistence
{
    internal const string CurrentMembershipIndexName =
        "ux_memberships_organization_user";

    internal const string CurrentMembershipFilter =
        "status IN ('" + MembershipStatusValues.Active + "', '"
        + MembershipStatusValues.Suspended + "')";

    internal static bool IsCurrentMembershipConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: CurrentMembershipIndexName,
        };
}
