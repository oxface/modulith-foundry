using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.Testing;

namespace ModulithFoundry.TopologyTests;

public sealed partial class LocalRuntimeTests
{
    private static readonly string[] InventoryManagerRoleIds = ["inventory-manager"];

    [Fact]
    public async Task MembershipAdministration_AuthenticatedHttpJourney_EnforcesLifecycleAndAccess()
    {
        using var timeout = new CancellationTokenSource(StartupTimeout);
        IDistributedApplicationTestingBuilder builder = await CreateBuilderAsync(
            randomizePorts: true,
            timeout.Token);

        await using DistributedApplication app = await builder.BuildAsync(timeout.Token);
        await app.StartAsync(timeout.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("api", timeout.Token);

        Uri apiAddress = app.GetEndpoint("api", "https");
        var administratorCookies = new CookieContainer();
        using var administratorHandler = CreateBrowserHandler(administratorCookies);
        using var administrator = new HttpClient(administratorHandler) { BaseAddress = apiAddress };
        string administratorCsrf = await SignInAsync(
            administrator,
            administratorCookies,
            "alice",
            "topology-user-password",
            timeout.Token);
        await CreateOrganizationInvitationAsync(
            administrator,
            administratorCsrf,
            "Membership Administration Organization",
            "membership-administration",
            "bob@example.test",
            timeout.Token);

        using HttpClient mailpit = app.CreateHttpClient("mailpit", "http");
        Uri acceptanceLink = await WaitForInvitationLinkAsync(
            mailpit,
            "bob@example.test",
            timeout.Token);
        var memberCookies = new CookieContainer();
        using var memberHandler = CreateBrowserHandler(memberCookies);
        using var member = new HttpClient(memberHandler) { BaseAddress = apiAddress };
        using HttpResponseMessage acceptanceChallenge = await member.GetAsync(
            acceptanceLink.PathAndQuery,
            timeout.Token);
        using HttpResponseMessage loginPage = await GetFollowingRedirectsAsync(
            member,
            memberCookies,
            Assert.IsType<Uri>(acceptanceChallenge.Headers.Location),
            timeout.Token);
        using HttpResponseMessage accepted = await SubmitKeycloakCredentialsAsync(
            member,
            memberCookies,
            loginPage,
            "bob",
            "topology-user-password",
            timeout.Token);
        Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
        Assert.Equal("/api/o/membership-administration", accepted.Headers.Location?.OriginalString);
        string memberCsrf = await GetCsrfTokenAsync(member, timeout.Token);

        using HttpResponseMessage memberList = await administrator.GetAsync(
            "/api/o/membership-administration/access/members",
            timeout.Token);
        Assert.Equal(HttpStatusCode.OK, memberList.StatusCode);
        using JsonDocument members = JsonDocument.Parse(
            await memberList.Content.ReadAsStringAsync(timeout.Token));
        JsonElement administratorMembership = Assert.Single(
            members.RootElement.GetProperty("members").EnumerateArray(),
            item => item.GetProperty("email").GetString() == "alice@example.test");
        JsonElement ordinaryMembership = Assert.Single(
            members.RootElement.GetProperty("members").EnumerateArray(),
            item => item.GetProperty("email").GetString() == "bob@example.test");
        Guid administratorMembershipId = administratorMembership
            .GetProperty("membershipId")
            .GetGuid();
        Guid ordinaryMembershipId = ordinaryMembership.GetProperty("membershipId").GetGuid();

        using HttpResponseMessage deniedList = await member.GetAsync(
            "/api/o/membership-administration/access/members",
            timeout.Token);
        await AssertProblemAsync(deniedList, HttpStatusCode.Forbidden, timeout.Token);

        using HttpResponseMessage missingCsrf = await member.PostAsync(
            $"/api/o/membership-administration/access/members/{ordinaryMembershipId:D}/suspend",
            content: null,
            timeout.Token);
        await AssertProblemAsync(missingCsrf, HttpStatusCode.BadRequest, timeout.Token);

        using HttpResponseMessage replacedRoles = await SendCommandAsync(
            administrator,
            HttpMethod.Put,
            $"/api/o/membership-administration/access/members/{ordinaryMembershipId:D}/roles",
            administratorCsrf,
            new { roleIds = InventoryManagerRoleIds },
            timeout.Token);
        Assert.Equal(HttpStatusCode.OK, replacedRoles.StatusCode);

        using HttpResponseMessage suspended = await SendCommandAsync(
            administrator,
            HttpMethod.Post,
            $"/api/o/membership-administration/access/members/{ordinaryMembershipId:D}/suspend",
            administratorCsrf,
            body: null,
            timeout.Token);
        await AssertMembershipStatusAsync(
            suspended,
            ordinaryMembershipId,
            "suspended",
            timeout.Token);
        using HttpResponseMessage suspendedAccess = await member.GetAsync(
            "/api/o/membership-administration",
            timeout.Token);
        await AssertProblemAsync(suspendedAccess, HttpStatusCode.NotFound, timeout.Token);

        using HttpResponseMessage reactivated = await SendCommandAsync(
            administrator,
            HttpMethod.Post,
            $"/api/o/membership-administration/access/members/{ordinaryMembershipId:D}/reactivate",
            administratorCsrf,
            body: null,
            timeout.Token);
        await AssertMembershipStatusAsync(
            reactivated,
            ordinaryMembershipId,
            "active",
            timeout.Token);
        using HttpResponseMessage restoredAccess = await member.GetAsync(
            "/api/o/membership-administration",
            timeout.Token);
        Assert.Equal(HttpStatusCode.OK, restoredAccess.StatusCode);
        using JsonDocument restoredScope = JsonDocument.Parse(
            await restoredAccess.Content.ReadAsStringAsync(timeout.Token));
        Assert.Equal(
            "inventory-manager",
            Assert.Single(restoredScope.RootElement.GetProperty("roleIds").EnumerateArray())
                .GetString());

        Guid foreignMembershipId = await CreateOrganizationAndReadMembershipIdAsync(
            member,
            memberCsrf,
            "Foreign Membership Organization",
            "foreign-membership",
            timeout.Token);
        using HttpResponseMessage crossOrganization = await SendCommandAsync(
            administrator,
            HttpMethod.Post,
            $"/api/o/membership-administration/access/members/{foreignMembershipId:D}/suspend",
            administratorCsrf,
            body: null,
            timeout.Token);
        await AssertProblemAsync(crossOrganization, HttpStatusCode.NotFound, timeout.Token);

        using HttpResponseMessage lastAdministrator = await SendCommandAsync(
            administrator,
            HttpMethod.Post,
            $"/api/o/membership-administration/access/members/{administratorMembershipId:D}/suspend",
            administratorCsrf,
            body: null,
            timeout.Token);
        await AssertProblemAsync(
            lastAdministrator,
            HttpStatusCode.Conflict,
            timeout.Token,
            expectedTitle: "Last organization administrator");

        using HttpResponseMessage removed = await SendCommandAsync(
            administrator,
            HttpMethod.Post,
            $"/api/o/membership-administration/access/members/{ordinaryMembershipId:D}/remove",
            administratorCsrf,
            body: null,
            timeout.Token);
        await AssertMembershipStatusAsync(
            removed,
            ordinaryMembershipId,
            "removed",
            timeout.Token);
        using HttpResponseMessage removedAccess = await member.GetAsync(
            "/api/o/membership-administration",
            timeout.Token);
        await AssertProblemAsync(removedAccess, HttpStatusCode.NotFound, timeout.Token);
        using HttpResponseMessage currentMembers = await administrator.GetAsync(
            "/api/o/membership-administration/access/members",
            timeout.Token);
        currentMembers.EnsureSuccessStatusCode();
        using JsonDocument currentMembersJson = JsonDocument.Parse(
            await currentMembers.Content.ReadAsStringAsync(timeout.Token));
        Assert.DoesNotContain(
            currentMembersJson.RootElement.GetProperty("members").EnumerateArray(),
            item => item.GetProperty("membershipId").GetGuid() == ordinaryMembershipId);
    }

    private static async Task<string> GetCsrfTokenAsync(
        HttpClient client,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage session = await client.GetAsync("/api/session", cancellationToken);
        session.EnsureSuccessStatusCode();
        using JsonDocument sessionJson = JsonDocument.Parse(
            await session.Content.ReadAsStringAsync(cancellationToken));
        return sessionJson.RootElement.GetProperty("csrfToken").GetString()
            ?? throw new InvalidOperationException("The session response has no CSRF token.");
    }

    private static async Task<Guid> CreateOrganizationAndReadMembershipIdAsync(
        HttpClient client,
        string csrfToken,
        string organizationName,
        string organizationSlug,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage created = await SendCommandAsync(
            client,
            HttpMethod.Post,
            "/api/organizations",
            csrfToken,
            new { name = organizationName, slug = organizationSlug },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using HttpResponseMessage members = await client.GetAsync(
            $"/api/o/{organizationSlug}/access/members",
            cancellationToken);
        members.EnsureSuccessStatusCode();
        using JsonDocument membersJson = JsonDocument.Parse(
            await members.Content.ReadAsStringAsync(cancellationToken));
        return Assert.Single(membersJson.RootElement.GetProperty("members").EnumerateArray())
            .GetProperty("membershipId")
            .GetGuid();
    }

    private static async Task<HttpResponseMessage> SendCommandAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string csrfToken,
        object? body,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request, cancellationToken);
    }

    private static async Task AssertMembershipStatusAsync(
        HttpResponseMessage response,
        Guid expectedMembershipId,
        string expectedStatus,
        CancellationToken cancellationToken)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument payload = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal(expectedMembershipId, payload.RootElement.GetProperty("membershipId").GetGuid());
        Assert.Equal(expectedStatus, payload.RootElement.GetProperty("status").GetString());
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        CancellationToken cancellationToken,
        string? expectedTitle = null)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using JsonDocument problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal((int)expectedStatus, problem.RootElement.GetProperty("status").GetInt32());
        if (expectedTitle is not null)
        {
            Assert.Equal(expectedTitle, problem.RootElement.GetProperty("title").GetString());
        }
    }
}
