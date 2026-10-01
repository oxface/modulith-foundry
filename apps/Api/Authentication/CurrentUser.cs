using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Api.Authentication;

internal sealed record CurrentUser(UserId UserId, string? Email, string? DisplayName);
