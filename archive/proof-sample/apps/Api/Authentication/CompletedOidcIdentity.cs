namespace ModulithFoundry.Api.Authentication;

internal sealed record CompletedOidcIdentity(
    CurrentUser CurrentUser,
    string? VerifiedProviderEmail
);
