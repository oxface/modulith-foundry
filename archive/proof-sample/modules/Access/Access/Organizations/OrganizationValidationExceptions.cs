namespace ModulithFoundry.Modules.Access.Organizations;

internal sealed class InvalidOrganizationNameException(string message) : Exception(message);

internal sealed class InvalidOrganizationSlugException(string message) : Exception(message);
