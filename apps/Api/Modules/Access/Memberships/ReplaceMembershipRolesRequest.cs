using System.ComponentModel.DataAnnotations;

namespace ModulithFoundry.Api.Modules.Access.Memberships;

public sealed record ReplaceMembershipRolesRequest(
    [property: Required] IReadOnlyList<string> RoleIds);
