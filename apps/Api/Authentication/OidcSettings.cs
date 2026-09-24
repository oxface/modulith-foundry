using System.ComponentModel.DataAnnotations;

namespace ModulithFoundry.Api.Authentication;

internal sealed class OidcSettings
{
    internal const string SectionName = "Authentication:Oidc";

    [Required]
    public string Authority { get; init; } = string.Empty;

    [Required]
    public string ClientId { get; init; } = string.Empty;

    [Required]
    public string ClientSecret { get; init; } = string.Empty;

    public bool RequireHttpsMetadata { get; init; } = true;
}
