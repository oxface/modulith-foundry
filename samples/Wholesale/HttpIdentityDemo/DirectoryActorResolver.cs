using System.Security.Claims;
using ModulithFoundry.ActorIdentity;
using ModulithFoundry.ActorIdentity.AspNetCore;

namespace ModulithFoundry.Samples.Wholesale.HttpIdentityDemo;

public sealed class DirectoryActorResolver(ExternalIdentityDirectory directory)
    : IHttpActorContextResolver
{
    public ValueTask<ActorContext?> ResolveAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        ClaimsIdentity[] identities = httpContext
            .User.Identities.Where(identity => identity.IsAuthenticated)
            .ToArray();
        ActorContext? actor = null;
        // This consumer admits exactly one authenticated identity with one issuer/subject pair.
        if (identities.Length == 1)
        {
            Claim[] issuers = identities[0].FindAll("iss").ToArray();
            Claim[] subjects = identities[0].FindAll("sub").ToArray();
            if (
                issuers.Length == 1
                && subjects.Length == 1
                && directory.Resolve(issuers[0].Value, subjects[0].Value) is { } userId
            )
                actor = new ActorContext(Actor.Human(userId));
        }
        return ValueTask.FromResult(actor);
    }
}
