using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using ModulithFoundry.Api.Authentication;

namespace ModulithFoundry.TopologyTests;

public sealed partial class LocalRuntimeTests
{
    private static readonly string[] InvitationRoleIds = ["sales-clerk"];

    [Fact]
    public async Task AuthenticatedJourney_KeycloakLogin_CreatesOrganizationAndSupportsLogout()
    {
        await using OtlpTestReceiver telemetry = await OtlpTestReceiver.StartAsync(
            TestContext.Current.CancellationToken);
        using var timeout = new CancellationTokenSource(StartupTimeout);
        IDistributedApplicationTestingBuilder builder = await CreateBuilderAsync(
            randomizePorts: true,
            timeout.Token);
        builder.CreateResourceBuilder<ProjectResource>("api")
            .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", telemetry.Endpoint.ToString())
            .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf");

        await using DistributedApplication app = await builder.BuildAsync(timeout.Token);
        await app.StartAsync(timeout.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("api", timeout.Token);

        Assert.True(app.ResourceNotifications.TryGetCurrentState("api", out ResourceEvent? api));
        EnvironmentVariableSnapshot authority = Assert.Single(
            api.Snapshot.EnvironmentVariables,
            variable => variable.Name == "Authentication__Oidc__Authority");
        Assert.StartsWith("https://localhost:", authority.Value, StringComparison.Ordinal);

        var cookies = new CookieContainer();
        using var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            CookieContainer = cookies,
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };
        using var client = new HttpClient(handler)
        {
            BaseAddress = app.GetEndpoint("api", "https"),
        };

        using HttpResponseMessage anonymousSession = await client.GetAsync(
            "/api/session",
            timeout.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousSession.StatusCode);
        using HttpResponseMessage anonymousOrganizations = await client.GetAsync(
            "/api/organizations",
            timeout.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousOrganizations.StatusCode);
        using HttpResponseMessage anonymousOrganizationScope = await client.GetAsync(
            "/api/o/topology-organization",
            timeout.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousOrganizationScope.StatusCode);

        using HttpResponseMessage challenge = await client.GetAsync(
            "/auth/login?returnUrl=/client-owned-route",
            timeout.Token);
        string challengeBody = await challenge.Content.ReadAsStringAsync(timeout.Token);
        Assert.True(
            challenge.StatusCode == HttpStatusCode.Redirect,
            $"Expected an OIDC redirect but received {(int)challenge.StatusCode}: {challengeBody}");

        using HttpResponseMessage loginPage = await GetFollowingRedirectsAsync(
            client,
            cookies,
            Assert.IsType<Uri>(challenge.Headers.Location),
            timeout.Token);
        string loginHtml = await loginPage.Content.ReadAsStringAsync(timeout.Token);
        Assert.True(
            loginPage.StatusCode == HttpStatusCode.OK,
            $"Expected the Keycloak login page but received {(int)loginPage.StatusCode} "
            + $"with Location '{loginPage.Headers.Location}': {loginHtml}");
        Match loginForm = KeycloakLoginForm().Match(loginHtml);
        Assert.True(loginForm.Success, "Keycloak login form was not found.");
        var loginAction = new Uri(WebUtility.HtmlDecode(loginForm.Groups["action"].Value));

        using var credentials = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = "alice",
            ["password"] = "topology-user-password",
            ["credentialId"] = string.Empty,
        });
        using HttpResponseMessage authenticatedRedirect = await client.PostAsync(
            loginAction,
            credentials,
            timeout.Token);
        StoreLoopbackSecureCookies(cookies, loginAction, authenticatedRedirect);
        string authenticationBody = await authenticatedRedirect.Content.ReadAsStringAsync(timeout.Token);
        Assert.Equal(HttpStatusCode.OK, authenticatedRedirect.StatusCode);
        using HttpResponseMessage callback = await SubmitOidcFormPostAsync(
            client,
            authenticationBody,
            timeout.Token);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/api/session", callback.Headers.Location?.OriginalString);
        string setCookie = Assert.Single(
            callback.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("__Host-modulith-foundry=", StringComparison.Ordinal));
        Assert.Contains("__Host-modulith-foundry=", setCookie, StringComparison.Ordinal);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alice@example.test", setCookie, StringComparison.OrdinalIgnoreCase);

        using HttpResponseMessage session = await client.GetAsync("/api/session", timeout.Token);
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
        using JsonDocument sessionJson = JsonDocument.Parse(
            await session.Content.ReadAsStringAsync(timeout.Token));
        Assert.NotEqual(
            Guid.Empty,
            sessionJson.RootElement.GetProperty("userId").GetGuid());
        Assert.Equal(
            "alice@example.test",
            sessionJson.RootElement.GetProperty("email").GetString());
        Assert.Equal(
            "Alice Example",
            sessionJson.RootElement.GetProperty("displayName").GetString());
        string csrfToken = sessionJson.RootElement.GetProperty("csrfToken").GetString()
            ?? throw new InvalidOperationException("The session response has no CSRF token.");

        using var invalidOrganizationRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/organizations")
        {
            Content = JsonContent.Create(new
            {
                name = "Invalid Organization",
                slug = "invalid/slug",
            }),
        };
        invalidOrganizationRequest.Headers.Add("X-CSRF-TOKEN", csrfToken);
        using HttpResponseMessage invalidOrganization = await client.SendAsync(
            invalidOrganizationRequest,
            timeout.Token);
        Assert.Equal(HttpStatusCode.BadRequest, invalidOrganization.StatusCode);

        using HttpResponseMessage createWithoutCsrf = await client.PostAsJsonAsync(
            "/api/organizations",
            new { name = "Topology Organization", slug = "Topology_Organization" },
            timeout.Token);
        Assert.Equal(HttpStatusCode.BadRequest, createWithoutCsrf.StatusCode);

        using var createOrganizationRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/organizations")
        {
            Content = JsonContent.Create(new
            {
                name = "Topology Organization",
                slug = "Topology_Organization",
            }),
        };
        createOrganizationRequest.Headers.Add("X-CSRF-TOKEN", csrfToken);
        using HttpResponseMessage createOrganization = await client.SendAsync(
            createOrganizationRequest,
            timeout.Token);
        Assert.Equal(HttpStatusCode.Created, createOrganization.StatusCode);
        using JsonDocument createdOrganization = JsonDocument.Parse(
            await createOrganization.Content.ReadAsStringAsync(timeout.Token));
        Assert.Equal(
            "topology-organization",
            createdOrganization.RootElement.GetProperty("slug").GetString());
        Assert.Equal(
            "organization-administrator",
            Assert.Single(createdOrganization.RootElement.GetProperty("roleIds").EnumerateArray())
                .GetString());

        using var duplicateOrganizationRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/organizations")
        {
            Content = JsonContent.Create(new
            {
                name = "Other Organization",
                slug = "topology-organization",
            }),
        };
        duplicateOrganizationRequest.Headers.Add("X-CSRF-TOKEN", csrfToken);
        using HttpResponseMessage duplicateOrganization = await client.SendAsync(
            duplicateOrganizationRequest,
            timeout.Token);
        Assert.Equal(HttpStatusCode.Conflict, duplicateOrganization.StatusCode);

        using HttpResponseMessage organizations = await client.GetAsync(
            "/api/organizations",
            timeout.Token);
        Assert.Equal(HttpStatusCode.OK, organizations.StatusCode);
        using JsonDocument organizationList = JsonDocument.Parse(
            await organizations.Content.ReadAsStringAsync(timeout.Token));
        JsonElement listedOrganization = Assert.Single(organizationList.RootElement.EnumerateArray());
        Assert.Equal(
            createdOrganization.RootElement.GetProperty("organizationId").GetGuid(),
            listedOrganization.GetProperty("organizationId").GetGuid());

        using HttpResponseMessage organizationScope = await client.GetAsync(
            "/api/o/topology-organization",
            timeout.Token);
        Assert.Equal(HttpStatusCode.OK, organizationScope.StatusCode);
        using JsonDocument organizationScopeJson = JsonDocument.Parse(
            await organizationScope.Content.ReadAsStringAsync(timeout.Token));
        Assert.Equal(
            createdOrganization.RootElement.GetProperty("organizationId").GetGuid(),
            organizationScopeJson.RootElement.GetProperty("organizationId").GetGuid());
        Assert.Equal(
            "organization-administrator",
            Assert.Single(organizationScopeJson.RootElement.GetProperty("roleIds").EnumerateArray())
                .GetString());

        using HttpResponseMessage inviteWithoutCsrf = await client.PostAsJsonAsync(
            "/api/o/topology-organization/invitations",
            new
            {
                recipientEmail = "invited.person@example.test",
                roleIds = InvitationRoleIds,
            },
            timeout.Token);
        Assert.Equal(HttpStatusCode.BadRequest, inviteWithoutCsrf.StatusCode);

        using var invitationRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/o/topology-organization/invitations")
        {
            Content = JsonContent.Create(new
            {
                recipientEmail = "invited.person@example.test",
                roleIds = InvitationRoleIds,
            }),
        };
        invitationRequest.Headers.Add("X-CSRF-TOKEN", csrfToken);
        using HttpResponseMessage invitation = await client.SendAsync(
            invitationRequest,
            timeout.Token);
        Assert.Equal(HttpStatusCode.Created, invitation.StatusCode);

        using HttpClient mailpit = app.CreateHttpClient("mailpit", "http");
        await WaitForInvitationEmailAsync(mailpit, timeout.Token);

        using HttpResponseMessage unknownOrganizationScope = await client.GetAsync(
            "/api/o/unknown-organization",
            timeout.Token);
        Assert.Equal(HttpStatusCode.NotFound, unknownOrganizationScope.StatusCode);

        Cookie authenticatedCookie = Assert.Single(
            cookies.GetCookies(Assert.IsType<Uri>(client.BaseAddress)).Cast<Cookie>(),
            cookie => cookie.Name == "__Host-modulith-foundry");
        string staleCookieValue = authenticatedCookie.Value;

        using HttpResponseMessage logoutWithoutCsrf = await client.PostAsync(
            "/auth/logout",
            content: null,
            timeout.Token);
        Assert.Equal(HttpStatusCode.BadRequest, logoutWithoutCsrf.StatusCode);

        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        logoutRequest.Headers.Add("X-CSRF-TOKEN", csrfToken);
        using HttpResponseMessage logout = await client.SendAsync(logoutRequest, timeout.Token);
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);

        cookies.Add(
            Assert.IsType<Uri>(client.BaseAddress),
            new Cookie("__Host-modulith-foundry", staleCookieValue, "/")
            {
                HttpOnly = true,
                Secure = true,
            });
        using HttpResponseMessage afterLogout = await client.GetAsync("/api/session", timeout.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);

        using HttpResponseMessage reauthenticationChallenge = await client.GetAsync(
            "/auth/login?returnUrl=/client-owned-route",
            timeout.Token);
        Assert.Equal(HttpStatusCode.Redirect, reauthenticationChallenge.StatusCode);
        using HttpResponseMessage ssoResponse = await GetFollowingRedirectsAsync(
            client,
            cookies,
            Assert.IsType<Uri>(reauthenticationChallenge.Headers.Location),
            timeout.Token);
        Assert.Equal(HttpStatusCode.OK, ssoResponse.StatusCode);
        string ssoResponseBody = await ssoResponse.Content.ReadAsStringAsync(timeout.Token);
        using HttpResponseMessage reauthenticatedCallback = await SubmitOidcFormPostAsync(
            client,
            ssoResponseBody,
            timeout.Token);
        Assert.Equal(HttpStatusCode.Redirect, reauthenticatedCallback.StatusCode);
        Task telemetryExport = telemetry.ExpectNextLogExportAsync(timeout.Token);

        using HttpResponseMessage reauthenticatedSession = await client.GetAsync(
            "/api/session",
            timeout.Token);
        Assert.Equal(HttpStatusCode.OK, reauthenticatedSession.StatusCode);

        ExecuteCommandResult stopRedis = await app.ResourceCommands.ExecuteCommandAsync(
            "redis",
            KnownResourceCommands.StopCommand,
            timeout.Token);
        Assert.True(stopRedis.Success, stopRedis.Message);
        await app.ResourceNotifications.WaitForResourceAsync(
            "redis",
            resource => resource.Snapshot.State?.Text == KnownResourceStates.Exited
                || resource.Snapshot.State?.Text == KnownResourceStates.Finished,
            timeout.Token);

        using HttpResponseMessage redisUnavailable = await client.GetAsync(
            "/api/session",
            timeout.Token);
        Assert.NotEqual(HttpStatusCode.OK, redisUnavailable.StatusCode);
        await telemetryExport;
        Assert.False(telemetry.ContainsLogText("topology-client-secret"));
        Assert.False(telemetry.ContainsLogText("topology-user-password"));
        Assert.False(telemetry.ContainsLogText("access_token"));
        Assert.False(telemetry.ContainsLogText("id_token"));
    }

    [GeneratedRegex(
        "<form[^>]+id=\"kc-form-login\"[^>]+action=\"(?<action>[^\"]+)\"",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex KeycloakLoginForm();

    [GeneratedRegex(
        "<form[^>]+method=\"post\"[^>]+action=\"(?<action>[^\"]+)\"",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OidcFormPost();

    [GeneratedRegex(
        "<input[^>]+type=\"hidden\"[^>]+name=\"(?<name>[^\"]+)\"[^>]+value=\"(?<value>[^\"]*)\"[^>]*/?>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OidcFormPostField();

    private static async Task<HttpResponseMessage> SubmitOidcFormPostAsync(
        HttpClient client,
        string responseBody,
        CancellationToken cancellationToken)
    {
        Match callbackForm = OidcFormPost().Match(responseBody);
        Assert.True(callbackForm.Success, "The OIDC form_post response was not found.");
        var callbackAction = new Uri(WebUtility.HtmlDecode(callbackForm.Groups["action"].Value));
        Dictionary<string, string> callbackFields = OidcFormPostField()
            .Matches(responseBody)
            .ToDictionary(
                match => WebUtility.HtmlDecode(match.Groups["name"].Value),
                match => WebUtility.HtmlDecode(match.Groups["value"].Value),
                StringComparer.Ordinal);

        using var callbackContent = new FormUrlEncodedContent(callbackFields);
        return await client.PostAsync(callbackAction, callbackContent, cancellationToken);
    }

    private static async Task<HttpResponseMessage> GetFollowingRedirectsAsync(
        HttpClient client,
        CookieContainer cookies,
        Uri location,
        CancellationToken cancellationToken)
    {
        Uri requestUri = location.IsAbsoluteUri
            ? location
            : new Uri(Assert.IsType<Uri>(client.BaseAddress), location);

        for (var redirect = 0; redirect < 5; redirect++)
        {
            HttpResponseMessage response = await client.GetAsync(requestUri, cancellationToken);
            StoreLoopbackSecureCookies(cookies, requestUri, response);
            if (response.StatusCode is not (
                HttpStatusCode.MovedPermanently
                or HttpStatusCode.Redirect
                or HttpStatusCode.RedirectMethod
                or HttpStatusCode.TemporaryRedirect
                or HttpStatusCode.PermanentRedirect))
            {
                return response;
            }

            Uri next = Assert.IsType<Uri>(response.Headers.Location);
            requestUri = next.IsAbsoluteUri ? next : new Uri(requestUri, next);
            response.Dispose();
        }

        throw new InvalidOperationException("Keycloak login exceeded the redirect limit.");
    }

    private static async Task WaitForInvitationEmailAsync(
        HttpClient mailpit,
        CancellationToken cancellationToken)
    {
        const string recipient = "invited.person@example.test";
        string searchPath = $"/api/v1/search?query={Uri.EscapeDataString($"to:{recipient}")}";

        while (true)
        {
            using HttpResponseMessage response = await mailpit.GetAsync(
                searchPath,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            using JsonDocument results = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken));
            JsonElement messages = results.RootElement.GetProperty("messages");
            if (messages.GetArrayLength() > 0)
            {
                JsonElement message = messages[0];
                Assert.Equal(
                    "Invitation to Topology Organization",
                    message.GetProperty("Subject").GetString());
                using HttpResponseMessage text = await mailpit.GetAsync(
                    $"/view/{message.GetProperty("ID").GetString()}.txt",
                    cancellationToken);
                string body = await text.Content.ReadAsStringAsync(cancellationToken);
                Assert.Contains("/invitations/accept?invitationId=", body, StringComparison.Ordinal);
                Assert.Contains("code=", body, StringComparison.Ordinal);
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
        }
    }

    private static void StoreLoopbackSecureCookies(
        CookieContainer cookies,
        Uri requestUri,
        HttpResponseMessage response)
    {
        if (!requestUri.IsLoopback
            || requestUri.Scheme != Uri.UriSchemeHttp
            || !response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values))
        {
            return;
        }

        // Browsers treat localhost as a secure context and accept these Keycloak cookies over HTTP.
        // CookieContainer does not implement that exception, so emulate it only in this test client.
        foreach (string value in values)
        {
            cookies.SetCookies(requestUri, SecureCookieAttribute().Replace(value, string.Empty));
        }
    }

    [GeneratedRegex(
        ";\\s*Secure(?=;|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecureCookieAttribute();
}
