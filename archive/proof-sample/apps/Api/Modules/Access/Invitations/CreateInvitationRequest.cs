using System.ComponentModel.DataAnnotations;

namespace ModulithFoundry.Api.Modules.Access.Invitations;

public sealed record CreateInvitationRequest(
    [property: Required, StringLength(320)] string RecipientEmail,
    [property: Required] IReadOnlyList<string> RoleIds
);
