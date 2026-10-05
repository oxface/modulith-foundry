using ModulithFoundry.Tenancy;
using ModulithFoundry.Tenancy.AspNetCore;

namespace ModulithFoundry.Samples.Wholesale.HttpIdentityDemo.Access;

internal sealed class OrganizationTenantResolver(OrganizationDirectory directory)
    : IHttpTenantCandidateResolver
{
    public ValueTask<TenantContext?> ResolveAsync(
        string? candidate,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        TenantContext? context =
            candidate is null ? TenantContext.Tenantless()
            : directory.Resolve(candidate) is { } tenant ? TenantContext.ForTenant(tenant)
            : null;
        return ValueTask.FromResult(context);
    }
}
