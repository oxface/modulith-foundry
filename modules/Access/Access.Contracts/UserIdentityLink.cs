namespace ModulithFoundry.Modules.Access.Contracts;

public sealed record UserIdentityLink(
    UserId UserId,
    string? Email,
    string? DisplayName);
