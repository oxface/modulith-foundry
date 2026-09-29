using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace ModulithFoundry.TopologyTests;

public sealed partial class LocalRuntimeTests
{
    [GeneratedRegex(@"https://[^\s]+/invitations/accept\?[^\s]+", RegexOptions.CultureInvariant)]
    private static partial Regex InvitationLink();

    [GeneratedRegex(
        "<a[^>]+href=\"(?<href>[^\"]+)\"[^>]*>\\s*Register\\s*</a>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex KeycloakRegistrationLink();

    [GeneratedRegex(
        "<form[^>]+id=\"kc-register-form\"[^>]+action=\"(?<action>[^\"]+)\"",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex KeycloakRegistrationForm();

    [GeneratedRegex(
        "<form[^>]+id=\"kc-passwd-update-form\"[^>]+action=\"(?<action>[^\"]+)\"",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex KeycloakPasswordUpdateForm();

    [GeneratedRegex(
        @"https?://[^\s]+/realms/[^\s]+/login-actions/[^\s]+",
        RegexOptions.CultureInvariant)]
    private static partial Regex KeycloakActionLink();

    [GeneratedRegex(
        "<a[^>]+href=\"(?<href>[^\"]+)\"",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HtmlLink();

    [Fact]
    public async Task InvitationAcceptance_AnonymousLink_ChallengesWithoutLeakingBearer()
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

        using var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };
        using var client = new HttpClient(handler)
        {
            BaseAddress = app.GetEndpoint("api", "https"),
        };
        const string bearer = "invitation-bearer-must-not-leak";

        Task nextLogExport = telemetry.ExpectNextLogExportAsync(timeout.Token);
        using HttpResponseMessage response = await client.GetAsync(
            $"/invitations/accept?invitationId={Guid.NewGuid():D}&code={bearer}",
            timeout.Token);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.DoesNotContain(
            bearer,
            Assert.IsType<Uri>(response.Headers.Location).OriginalString,
            StringComparison.Ordinal);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        await telemetry.WaitForTraceTextAsync("/invitations/accept", timeout.Token);
        await nextLogExport;
        Assert.False(telemetry.ContainsTraceText(bearer), telemetry.TraceContext(bearer));
        Assert.False(telemetry.ContainsLogText(bearer), telemetry.LogContext(bearer));
    }

    [Fact]
    public async Task InvitationAcceptance_DirectoryGatedRejectionLeavesInvitationForPreprovisionedUser()
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
        string csrfToken = await SignInAsync(
            administrator,
            administratorCookies,
            "alice",
            "topology-user-password",
            timeout.Token);

        await CreateOrganizationInvitationAsync(
            administrator,
            csrfToken,
            "Invitation Acceptance Organization",
            "invitation-acceptance",
            "bob@example.test",
            timeout.Token);

        using HttpClient mailpit = app.CreateHttpClient("mailpit", "http");
        Uri acceptanceLink = await WaitForInvitationLinkAsync(
            mailpit,
            "bob@example.test",
            timeout.Token);
        var rejectedCookies = new CookieContainer();
        using var rejectedHandler = CreateBrowserHandler(rejectedCookies);
        using var rejected = new HttpClient(rejectedHandler) { BaseAddress = apiAddress };
        using HttpResponseMessage rejectedChallenge = await rejected.GetAsync(
            acceptanceLink.PathAndQuery,
            timeout.Token);
        using HttpResponseMessage rejectedLoginPage = await GetFollowingRedirectsAsync(
            rejected,
            rejectedCookies,
            Assert.IsType<Uri>(rejectedChallenge.Headers.Location),
            timeout.Token);
        using HttpResponseMessage rejectedLogin = await PostKeycloakCredentialsAsync(
            rejected,
            rejectedCookies,
            rejectedLoginPage,
            "not-provisioned",
            "invalid-password",
            timeout.Token);
        Assert.Equal(HttpStatusCode.OK, rejectedLogin.StatusCode);
        Assert.True(
            KeycloakLoginForm().IsMatch(await rejectedLogin.Content.ReadAsStringAsync(timeout.Token)),
            "Directory-gated Keycloak should keep an unknown user on the login page.");

        using HttpResponseMessage mismatchedChallenge = await administrator.GetAsync(
            acceptanceLink.PathAndQuery,
            timeout.Token);
        using HttpResponseMessage mismatchedIdentity = await GetFollowingRedirectsAsync(
            administrator,
            administratorCookies,
            Assert.IsType<Uri>(mismatchedChallenge.Headers.Location),
            timeout.Token);
        using HttpResponseMessage mismatchedCallback = await SubmitOidcFormPostAsync(
            administrator,
            await mismatchedIdentity.Content.ReadAsStringAsync(timeout.Token),
            timeout.Token);
        Assert.Equal(HttpStatusCode.Redirect, mismatchedCallback.StatusCode);
        Assert.StartsWith(
            "/invitations/accept/result?status=recipient-mismatch",
            mismatchedCallback.Headers.Location?.OriginalString,
            StringComparison.Ordinal);
        using HttpResponseMessage mismatch = await administrator.GetAsync(
            mismatchedCallback.Headers.Location,
            timeout.Token);
        Assert.Equal(HttpStatusCode.Forbidden, mismatch.StatusCode);
        using JsonDocument mismatchProblem = JsonDocument.Parse(
            await mismatch.Content.ReadAsStringAsync(timeout.Token));
        string retryPath = mismatchProblem.RootElement.GetProperty("retry").GetString()
            ?? throw new InvalidOperationException("The mismatch response has no retry path.");

        using var retryRequest = new HttpRequestMessage(HttpMethod.Post, retryPath);
        retryRequest.Headers.Add("X-CSRF-TOKEN", csrfToken);
        using HttpResponseMessage acceptanceChallenge = await administrator.SendAsync(
            retryRequest,
            timeout.Token);
        Assert.Equal(HttpStatusCode.Redirect, acceptanceChallenge.StatusCode);
        using HttpResponseMessage loginPage = await GetFollowingRedirectsAsync(
            administrator,
            administratorCookies,
            Assert.IsType<Uri>(acceptanceChallenge.Headers.Location),
            timeout.Token);
        Assert.True(
            KeycloakLoginForm().IsMatch(await loginPage.Content.ReadAsStringAsync(timeout.Token)),
            "Invitation retry should require an explicit identity selection.");
        using HttpResponseMessage callback = await SubmitKeycloakCredentialsAsync(
            administrator,
            administratorCookies,
            loginPage,
            "bob",
            "topology-user-password",
            timeout.Token);

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/api/o/invitation-acceptance", callback.Headers.Location?.OriginalString);
        using HttpResponseMessage organizationScope = await administrator.GetAsync(
            callback.Headers.Location,
            timeout.Token);
        Assert.Equal(HttpStatusCode.OK, organizationScope.StatusCode);
        using JsonDocument scope = JsonDocument.Parse(
            await organizationScope.Content.ReadAsStringAsync(timeout.Token));
        Assert.Equal(
            "sales-clerk",
            Assert.Single(scope.RootElement.GetProperty("roleIds").EnumerateArray()).GetString());
    }

    [Fact]
    public async Task InvitationAcceptance_OpenRegistration_VerifiesEmailAndBecomesMember()
    {
        using var timeout = new CancellationTokenSource(StartupTimeout);
        IDistributedApplicationTestingBuilder builder = await CreateBuilderAsync(
            randomizePorts: true,
            timeout.Token);
        IResourceBuilder<KeycloakResource> keycloak =
            builder.CreateResourceBuilder<KeycloakResource>("keycloak");
        builder.CreateResourceBuilder<ProjectResource>("api")
            .WithEnvironment(
                "Authentication__Oidc__Authority",
                ReferenceExpression.Create(
                    $"{keycloak.GetEndpoint("http")}/realms/modulith-foundry-open"));

        await using DistributedApplication app = await builder.BuildAsync(timeout.Token);
        await app.StartAsync(timeout.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("api", timeout.Token);

        Uri apiAddress = app.GetEndpoint("api", "https");
        var administratorCookies = new CookieContainer();
        using var administratorHandler = CreateBrowserHandler(administratorCookies);
        using var administrator = new HttpClient(administratorHandler) { BaseAddress = apiAddress };
        string csrfToken = await SignInAsync(
            administrator,
            administratorCookies,
            "alice",
            "topology-user-password",
            timeout.Token);
        await CreateOrganizationInvitationAsync(
            administrator,
            csrfToken,
            "Open Registration Organization",
            "open-registration",
            "charlie@example.test",
            timeout.Token);

        using HttpClient mailpit = app.CreateHttpClient("mailpit", "http");
        Uri acceptanceLink = await WaitForInvitationLinkAsync(
            mailpit,
            "charlie@example.test",
            timeout.Token);
        var recipientCookies = new CookieContainer();
        using var recipientHandler = CreateBrowserHandler(recipientCookies);
        using var recipient = new HttpClient(recipientHandler) { BaseAddress = apiAddress };
        using HttpResponseMessage acceptanceChallenge = await recipient.GetAsync(
            acceptanceLink.PathAndQuery,
            timeout.Token);
        using HttpResponseMessage loginPage = await GetFollowingRedirectsAsync(
            recipient,
            recipientCookies,
            Assert.IsType<Uri>(acceptanceChallenge.Headers.Location),
            timeout.Token);
        string loginHtml = await loginPage.Content.ReadAsStringAsync(timeout.Token);
        Match registrationLink = KeycloakRegistrationLink().Match(loginHtml);
        Assert.True(registrationLink.Success, "Keycloak registration link was not found.");
        var registrationUri = new Uri(
            loginPage.RequestMessage?.RequestUri
                ?? throw new InvalidOperationException("The Keycloak login page has no request URI."),
            WebUtility.HtmlDecode(registrationLink.Groups["href"].Value));
        using HttpResponseMessage registrationPage = await recipient.GetAsync(
            registrationUri,
            timeout.Token);
        string registrationHtml = await registrationPage.Content.ReadAsStringAsync(timeout.Token);
        Match registrationForm = KeycloakRegistrationForm().Match(registrationHtml);
        Assert.True(registrationForm.Success, "Keycloak registration form was not found.");
        var registrationAction = new Uri(
            WebUtility.HtmlDecode(registrationForm.Groups["action"].Value));
        using var registration = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["firstName"] = "Charlie",
            ["lastName"] = "Example",
            ["email"] = "charlie@example.test",
            ["username"] = "charlie",
            ["password"] = "open-registration-password",
            ["password-confirm"] = "open-registration-password",
        });
        using HttpResponseMessage registered = await recipient.PostAsync(
            registrationAction,
            registration,
            timeout.Token);
        StoreLoopbackSecureCookies(recipientCookies, registrationAction, registered);
        Assert.Equal(HttpStatusCode.Redirect, registered.StatusCode);
        using HttpResponseMessage verificationNotice = await GetFollowingRedirectsAsync(
            recipient,
            recipientCookies,
            Assert.IsType<Uri>(registered.Headers.Location),
            timeout.Token);
        Assert.Equal(HttpStatusCode.OK, verificationNotice.StatusCode);

        Uri verificationLink = await WaitForKeycloakActionLinkAsync(
            mailpit,
            "charlie@example.test",
            timeout.Token);
        using HttpResponseMessage verification = await GetFollowingRedirectsAsync(
            recipient,
            recipientCookies,
            verificationLink,
            timeout.Token);
        string verificationBody = await verification.Content.ReadAsStringAsync(timeout.Token);
        Match passwordUpdate = KeycloakPasswordUpdateForm().Match(verificationBody);
        if (passwordUpdate.Success)
        {
            var passwordAction = new Uri(
                verification.RequestMessage?.RequestUri
                    ?? throw new InvalidOperationException("The Keycloak password page has no request URI."),
                WebUtility.HtmlDecode(passwordUpdate.Groups["action"].Value));
            using var passwords = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["password-new"] = "open-registration-password",
                ["password-confirm"] = "open-registration-password",
            });
            using HttpResponseMessage passwordUpdated = await recipient.PostAsync(
                passwordAction,
                passwords,
                timeout.Token);
            StoreLoopbackSecureCookies(recipientCookies, passwordAction, passwordUpdated);
            if (passwordUpdated.StatusCode is HttpStatusCode.Redirect)
            {
                using HttpResponseMessage resumed = await GetFollowingRedirectsAsync(
                    recipient,
                    recipientCookies,
                    Assert.IsType<Uri>(passwordUpdated.Headers.Location),
                    timeout.Token);
                verificationBody = await resumed.Content.ReadAsStringAsync(timeout.Token);
            }
            else
            {
                Assert.Equal(HttpStatusCode.OK, passwordUpdated.StatusCode);
                verificationBody = await passwordUpdated.Content.ReadAsStringAsync(timeout.Token);
            }
        }

        if (!OidcFormPost().IsMatch(verificationBody))
        {
            Uri resumeUri = FindKeycloakResumeUri(verification, verificationBody);
            using HttpResponseMessage resumed = await GetFollowingRedirectsAsync(
                recipient,
                recipientCookies,
                resumeUri,
                timeout.Token);
            verificationBody = await resumed.Content.ReadAsStringAsync(timeout.Token);
        }

        using HttpResponseMessage callback = await SubmitOidcFormPostAsync(
            recipient,
            verificationBody,
            timeout.Token);

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/api/o/open-registration", callback.Headers.Location?.OriginalString);
        using HttpResponseMessage organizationScope = await recipient.GetAsync(
            callback.Headers.Location,
            timeout.Token);
        Assert.Equal(HttpStatusCode.OK, organizationScope.StatusCode);
    }

    private static HttpClientHandler CreateBrowserHandler(CookieContainer cookies) =>
        new()
        {
            AllowAutoRedirect = false,
            CookieContainer = cookies,
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };

    private static async Task CreateOrganizationInvitationAsync(
        HttpClient administrator,
        string csrfToken,
        string organizationName,
        string organizationSlug,
        string recipientEmail,
        CancellationToken cancellationToken)
    {
        using var createOrganization = new HttpRequestMessage(HttpMethod.Post, "/api/organizations")
        {
            Content = JsonContent.Create(new { name = organizationName, slug = organizationSlug }),
        };
        createOrganization.Headers.Add("X-CSRF-TOKEN", csrfToken);
        using HttpResponseMessage organization = await administrator.SendAsync(
            createOrganization,
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, organization.StatusCode);

        using var createInvitation = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/o/{organizationSlug}/invitations")
        {
            Content = JsonContent.Create(new
            {
                recipientEmail,
                roleIds = InvitationRoleIds,
            }),
        };
        createInvitation.Headers.Add("X-CSRF-TOKEN", csrfToken);
        using HttpResponseMessage invitation = await administrator.SendAsync(
            createInvitation,
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, invitation.StatusCode);
    }

    private static async Task<string> SignInAsync(
        HttpClient client,
        CookieContainer cookies,
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage challenge = await client.GetAsync(
            "/auth/login",
            cancellationToken);
        using HttpResponseMessage loginPage = await GetFollowingRedirectsAsync(
            client,
            cookies,
            Assert.IsType<Uri>(challenge.Headers.Location),
            cancellationToken);
        using HttpResponseMessage callback = await SubmitKeycloakCredentialsAsync(
            client,
            cookies,
            loginPage,
            username,
            password,
            cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);

        using HttpResponseMessage session = await client.GetAsync("/api/session", cancellationToken);
        session.EnsureSuccessStatusCode();
        using JsonDocument sessionJson = JsonDocument.Parse(
            await session.Content.ReadAsStringAsync(cancellationToken));
        return sessionJson.RootElement.GetProperty("csrfToken").GetString()
            ?? throw new InvalidOperationException("The session response has no CSRF token.");
    }

    private static async Task<HttpResponseMessage> SubmitKeycloakCredentialsAsync(
        HttpClient client,
        CookieContainer cookies,
        HttpResponseMessage loginPage,
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage authenticated = await PostKeycloakCredentialsAsync(
            client,
            cookies,
            loginPage,
            username,
            password,
            cancellationToken);
        string authenticationBody = await authenticated.Content.ReadAsStringAsync(cancellationToken);
        return await SubmitOidcFormPostAsync(client, authenticationBody, cancellationToken);
    }

    private static async Task<HttpResponseMessage> PostKeycloakCredentialsAsync(
        HttpClient client,
        CookieContainer cookies,
        HttpResponseMessage loginPage,
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        string loginHtml = await loginPage.Content.ReadAsStringAsync(cancellationToken);
        Match loginForm = KeycloakLoginForm().Match(loginHtml);
        Assert.True(loginForm.Success, "Keycloak login form was not found.");
        var loginAction = new Uri(WebUtility.HtmlDecode(loginForm.Groups["action"].Value));
        using var credentials = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = username,
            ["password"] = password,
            ["credentialId"] = string.Empty,
        });
        HttpResponseMessage response = await client.PostAsync(
            loginAction,
            credentials,
            cancellationToken);
        StoreLoopbackSecureCookies(cookies, loginAction, response);
        return response;
    }

    private static async Task<Uri> WaitForInvitationLinkAsync(
        HttpClient mailpit,
        string recipient,
        CancellationToken cancellationToken)
    {
        string searchPath = $"/api/v1/search?query={Uri.EscapeDataString($"to:{recipient}")}";
        while (true)
        {
            using HttpResponseMessage response = await mailpit.GetAsync(searchPath, cancellationToken);
            response.EnsureSuccessStatusCode();
            using JsonDocument results = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken));
            JsonElement messages = results.RootElement.GetProperty("messages");
            if (messages.GetArrayLength() > 0)
            {
                string messageId = messages[0].GetProperty("ID").GetString()
                    ?? throw new InvalidOperationException("The invitation email has no ID.");
                using HttpResponseMessage text = await mailpit.GetAsync(
                    $"/view/{messageId}.txt",
                    cancellationToken);
                string body = await text.Content.ReadAsStringAsync(cancellationToken);
                Match link = InvitationLink().Match(body);
                Assert.True(link.Success, "The invitation acceptance link was not found.");
                return new Uri(link.Value);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
        }
    }

    private static async Task<Uri> WaitForKeycloakActionLinkAsync(
        HttpClient mailpit,
        string recipient,
        CancellationToken cancellationToken)
    {
        string searchPath = $"/api/v1/search?query={Uri.EscapeDataString($"to:{recipient}")}";
        while (true)
        {
            using HttpResponseMessage response = await mailpit.GetAsync(searchPath, cancellationToken);
            response.EnsureSuccessStatusCode();
            using JsonDocument results = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken));
            foreach (JsonElement message in results.RootElement.GetProperty("messages").EnumerateArray())
            {
                string messageId = message.GetProperty("ID").GetString()
                    ?? throw new InvalidOperationException("The Keycloak email has no ID.");
                using HttpResponseMessage text = await mailpit.GetAsync(
                    $"/view/{messageId}.txt",
                    cancellationToken);
                string body = await text.Content.ReadAsStringAsync(cancellationToken);
                Match link = KeycloakActionLink().Match(body);
                if (link.Success)
                {
                    return new Uri(WebUtility.HtmlDecode(link.Value));
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
        }
    }

    private static Uri FindKeycloakResumeUri(HttpResponseMessage response, string html)
    {
        foreach (Match link in HtmlLink().Matches(html))
        {
            string value = WebUtility.HtmlDecode(link.Groups["href"].Value);
            if (value.Contains("login-actions", StringComparison.Ordinal)
                || value.Contains("openid-connect", StringComparison.Ordinal))
            {
                return new Uri(
                    response.RequestMessage?.RequestUri
                        ?? throw new InvalidOperationException("The Keycloak response has no request URI."),
                    value);
            }
        }

        throw new InvalidOperationException(
            $"The Keycloak verification page at '{response.RequestMessage?.RequestUri}' has no resume link.");
    }
}
