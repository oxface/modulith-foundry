using System.ComponentModel.DataAnnotations;

namespace ModulithFoundry.Api.Modules.Access.Organizations;

public sealed record CreateOrganizationRequest(
    [property: Required, StringLength(200)] string Name,
    [property: Required, StringLength(63)] string Slug);
