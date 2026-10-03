using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ModulithFoundry.Api.Authentication;
using ModulithFoundry.Api.Modules.Access.Middleware;
using ModulithFoundry.Api.Modules.Access.Organizations;
using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.ApplicationTests;

public sealed class OrganizationScopeMiddlewareTests
{
    [Fact]
    public async Task Invoke_OrganizationScopedEndpoint_ResolvesAndAttachesAccessContext()
    {
        var userId = new UserId(Guid.Parse("01997d3d-8d8c-7c31-b74b-8dafc2bb2a01"));
        var organizationContext = new OrganizationAccessContext(
            userId,
            new OrganizationId(Guid.Parse("01997d3d-8d8c-7c31-b74b-8dafc2bb2a02")),
            new MembershipId(Guid.Parse("01997d3d-8d8c-7c31-b74b-8dafc2bb2a03")),
            "Scoped Organization",
            "scoped-organization",
            [SystemRoleIds.OrganizationAdministrator]
        );
        var queries = new OrganizationQueriesStub(organizationContext);
        var accessor = new OrganizationContextAccessor();
        bool nextCalled = false;
        var middleware = new OrganizationScopeMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        DefaultHttpContext httpContext = CreateHttpContext(userId, "scoped-organization");

        await middleware.InvokeAsync(httpContext, accessor, queries);

        Assert.True(nextCalled);
        Assert.Same(organizationContext, accessor.OrganizationContext);
        Assert.Equal(userId, queries.UserId);
        Assert.Equal("scoped-organization", queries.OrganizationSlug);
    }

    [Fact]
    public async Task Invoke_InaccessibleOrganization_ReturnsNotFoundWithoutCallingEndpoint()
    {
        var userId = new UserId(Guid.Parse("01997d3d-8d8c-7c31-b74b-8dafc2bb2a01"));
        var queries = new OrganizationQueriesStub(organizationContext: null);
        var accessor = new OrganizationContextAccessor();
        bool nextCalled = false;
        var middleware = new OrganizationScopeMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        DefaultHttpContext httpContext = CreateHttpContext(userId, "inaccessible-organization");

        await middleware.InvokeAsync(httpContext, accessor, queries);

        Assert.Equal(StatusCodes.Status404NotFound, httpContext.Response.StatusCode);
        Assert.False(nextCalled);
        Assert.Null(accessor.OrganizationContext);
    }

    private static DefaultHttpContext CreateHttpContext(UserId userId, string organizationSlug)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim(ProductClaims.UserId, userId.Value.ToString())],
                    "test"
                )
            ),
        };
        httpContext.Request.RouteValues["organizationSlug"] = organizationSlug;
        httpContext.SetEndpoint(
            new Endpoint(
                _ => Task.CompletedTask,
                new EndpointMetadataCollection(OrganizationScopeMetadata.Instance),
                "organization-scoped test endpoint"
            )
        );
        return httpContext;
    }

    private sealed class OrganizationQueriesStub(OrganizationAccessContext? organizationContext)
        : IOrganizationQueries
    {
        internal UserId? UserId { get; private set; }

        internal string? OrganizationSlug { get; private set; }

        public Task<IReadOnlyList<OrganizationMembership>> ListAccessibleToAsync(
            UserId userId,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public Task<OrganizationAccessContext?> ResolveAccessAsync(
            UserId userId,
            string organizationSlug,
            CancellationToken cancellationToken = default
        )
        {
            UserId = userId;
            OrganizationSlug = organizationSlug;
            return Task.FromResult<OrganizationAccessContext?>(organizationContext);
        }
    }
}
