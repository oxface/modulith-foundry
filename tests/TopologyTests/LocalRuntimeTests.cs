using System.Net;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace ModulithFoundry.TopologyTests;

public sealed class LocalRuntimeTests
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(2);

    [Fact]
    public async Task LocalRuntime_RepeatedLifecycle_ProvidesHealthyApiAfterMigrations()
    {
        await using OtlpTestReceiver telemetry = await OtlpTestReceiver.StartAsync(
            TestContext.Current.CancellationToken);

        for (var iteration = 0; iteration < 2; iteration++)
        {
            using var timeout = new CancellationTokenSource(StartupTimeout);
            IDistributedApplicationTestingBuilder builder = await CreateBuilderAsync(
                randomizePorts: false,
                timeout.Token);
            builder.CreateResourceBuilder<ProjectResource>("api")
                .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", telemetry.Endpoint.ToString())
                .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf");
            Task telemetryExport = telemetry.ExpectNextLogExportAsync(timeout.Token);

            await using DistributedApplication app = await builder.BuildAsync(timeout.Token);
            await app.StartAsync(timeout.Token);

            await app.ResourceNotifications.WaitForResourceAsync(
                "migrator",
                KnownResourceStates.Finished,
                timeout.Token);
            await app.ResourceNotifications.WaitForResourceHealthyAsync("api", timeout.Token);

            Assert.True(app.ResourceNotifications.TryGetCurrentState("api", out ResourceEvent? api));
            Assert.Contains(
                api.Snapshot.EnvironmentVariables,
                environment => environment.Name == "OTEL_EXPORTER_OTLP_ENDPOINT");

            using HttpClient client = app.CreateHttpClient("api");
            using HttpResponseMessage readiness = await client.GetAsync("/health", timeout.Token);
            using HttpResponseMessage liveness = await client.GetAsync("/alive", timeout.Token);

            Assert.Equal(HttpStatusCode.OK, readiness.StatusCode);
            Assert.Equal(HttpStatusCode.OK, liveness.StatusCode);
            await telemetryExport;
        }
    }

    [Fact]
    public async Task Migrator_InvalidConnection_BlocksApiStartup()
    {
        using var timeout = new CancellationTokenSource(StartupTimeout);
        IDistributedApplicationTestingBuilder builder = await CreateBuilderAsync(
            randomizePorts: true,
            timeout.Token);

        builder.CreateResourceBuilder<ProjectResource>("migrator")
            .WithEnvironment(
                "ConnectionStrings__database",
                "Host=127.0.0.1;Port=1;Database=invalid;Username=invalid;Password=invalid;Timeout=1");

        await using DistributedApplication app = await builder.BuildAsync(timeout.Token);
        await app.StartAsync(timeout.Token);

        await app.ResourceNotifications.WaitForResourceAsync(
            "migrator",
            KnownResourceStates.Finished,
            timeout.Token);

        Assert.True(app.ResourceNotifications.TryGetCurrentState("migrator", out ResourceEvent? migrator));
        Assert.NotEqual(0, migrator.Snapshot.ExitCode);
        Assert.True(app.ResourceNotifications.TryGetCurrentState("api", out ResourceEvent? api));
        Assert.NotEqual(KnownResourceStates.Running, api.Snapshot.State?.Text);
        Assert.NotEqual(KnownResourceStates.Finished, api.Snapshot.State?.Text);
    }

    [Fact]
    public async Task Health_DatabaseUnavailable_ReportsUnreadyButAlive()
    {
        using var timeout = new CancellationTokenSource(StartupTimeout);
        IDistributedApplicationTestingBuilder builder = await CreateBuilderAsync(
            randomizePorts: true,
            timeout.Token);

        builder.CreateResourceBuilder<ProjectResource>("api")
            .WithEnvironment(
                "ConnectionStrings__database",
                "Host=127.0.0.1;Port=1;Database=invalid;Username=invalid;Password=invalid;Timeout=1;Command Timeout=1");

        await using DistributedApplication app = await builder.BuildAsync(timeout.Token);
        await app.StartAsync(timeout.Token);

        await app.ResourceNotifications.WaitForResourceAsync(
            "migrator",
            KnownResourceStates.Finished,
            timeout.Token);
        await app.ResourceNotifications.WaitForResourceAsync(
            "api",
            KnownResourceStates.Running,
            timeout.Token);

        using HttpClient client = app.CreateHttpClient("api");
        using HttpResponseMessage readiness = await client.GetAsync("/health", timeout.Token);
        using HttpResponseMessage liveness = await client.GetAsync("/alive", timeout.Token);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, readiness.StatusCode);
        Assert.Equal(HttpStatusCode.OK, liveness.StatusCode);
    }

    private static async Task<IDistributedApplicationTestingBuilder> CreateBuilderAsync(
        bool randomizePorts,
        CancellationToken cancellationToken)
    {
        IDistributedApplicationTestingBuilder builder =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.ModulithFoundry_AppHost>(
                cancellationToken);

        builder.Configuration["Parameters:postgres-password"] = "topology-test-password";
        builder.Configuration["DcpPublisher:RandomizePorts"] = randomizePorts.ToString();

        PostgresServerResource postgres = builder.CreateResourceBuilder<PostgresServerResource>("postgres")
            .Resource;
        foreach (ContainerMountAnnotation volume in postgres.Annotations
                     .OfType<ContainerMountAnnotation>()
                     .Where(static mount => mount.Type == ContainerMountType.Volume)
                     .ToArray())
        {
            postgres.Annotations.Remove(volume);
        }

        return builder;
    }
}
