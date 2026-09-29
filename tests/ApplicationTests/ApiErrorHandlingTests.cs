using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModulithFoundry.Api.Infrastructure;
using ModulithFoundry.Api.Modules.Access.Invitations;
using ModulithFoundry.Api.Modules.Access.Organizations;

namespace ModulithFoundry.ApplicationTests;

public sealed class ApiErrorHandlingTests
{
    [Fact]
    public async Task ApiErrors_EmptyNotFound_ReturnsProblemDetails()
    {
        await using WebApplication app = await StartApplicationAsync(static application =>
        {
            application.MapGet("/missing", static () => Results.NotFound());
        });

        using HttpClient client = app.GetTestClient();
        using HttpResponseMessage response = await client.GetAsync(
            "/missing",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using JsonDocument problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(404, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Not Found", problem.RootElement.GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(
            problem.RootElement.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task ApiErrors_UnexpectedException_ReturnsGenericProblemDetails()
    {
        await using WebApplication app = await StartApplicationAsync(static application =>
        {
            application.MapGet("/failure", static IResult () =>
                throw new InvalidOperationException("sensitive failure detail"));
        });

        using HttpClient client = app.GetTestClient();
        using HttpResponseMessage response = await client.GetAsync(
            "/failure?secret=must-not-leak",
            TestContext.Current.CancellationToken);
        string responseBody = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using JsonDocument problem = JsonDocument.Parse(responseBody);
        Assert.Equal(500, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(
            "An error occurred while processing your request.",
            problem.RootElement.GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(
            problem.RootElement.GetProperty("traceId").GetString()));
        Assert.DoesNotContain("sensitive failure detail", responseBody, StringComparison.Ordinal);
        Assert.DoesNotContain("must-not-leak", responseBody, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", responseBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestValidation_MissingRequiredCollection_ReturnsValidationProblemDetails()
    {
        await using WebApplication app = await StartApplicationAsync(static application =>
        {
            application.MapPost(
                "/invitations",
                static (CreateInvitationRequest request) => Results.Ok(request));
        });

        using HttpClient client = app.GetTestClient();
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/invitations",
            new
            {
                recipientEmail = "invited.person@example.test",
                roleIds = (string[]?)null,
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using JsonDocument problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("RoleIds", out _));
    }

    [Fact]
    public async Task RequestValidation_MissingRequiredString_ReturnsValidationProblemDetails()
    {
        await using WebApplication app = await StartApplicationAsync(static application =>
        {
            application.MapPost(
                "/organizations",
                static (CreateOrganizationRequest request) => Results.Ok(request));
        });

        using HttpClient client = app.GetTestClient();
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/organizations",
            new
            {
                name = (string?)null,
                slug = "valid-organization",
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using JsonDocument problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("Name", out _));
    }

    private static async Task<WebApplication> StartApplicationAsync(
        Action<WebApplication> mapEndpoints)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production,
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        builder.Services.AddApiErrorHandling();
        builder.Services.AddApiRequestValidation();

        WebApplication app = builder.Build();
        app.UseApiErrorHandling();
        mapEndpoints(app);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
