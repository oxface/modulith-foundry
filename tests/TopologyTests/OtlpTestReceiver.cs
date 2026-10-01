using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ModulithFoundry.TopologyTests;

internal sealed class OtlpTestReceiver : IAsyncDisposable
{
    private readonly WebApplication application;
    private readonly List<byte[]> logPayloads = [];
    private readonly List<byte[]> tracePayloads = [];
    private readonly Lock sync = new();
    private TaskCompletionSource? nextLogExport;
    private TaskCompletionSource? nextTraceExport;

    private OtlpTestReceiver(WebApplication application)
    {
        this.application = application;
    }

    internal Uri Endpoint { get; private set; } = null!;

    internal static async Task<OtlpTestReceiver> StartAsync(CancellationToken cancellationToken)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        WebApplication application = builder.Build();
        var receiver = new OtlpTestReceiver(application);

        application.MapPost("/v1/logs", receiver.ReceiveLogsAsync);
        application.MapPost("/v1/traces", receiver.ReceiveTracesAsync);
        await application.StartAsync(cancellationToken);

        IServerAddressesFeature addresses =
            application
                .Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException(
                "The OTLP test receiver has no server addresses."
            );
        receiver.Endpoint = new Uri(addresses.Addresses.Single(), UriKind.Absolute);
        return receiver;
    }

    internal Task ExpectNextLogExportAsync(CancellationToken cancellationToken)
    {
        lock (sync)
        {
            if (nextLogExport is not null)
            {
                throw new InvalidOperationException("A log export expectation is already pending.");
            }

            nextLogExport = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            return nextLogExport.Task.WaitAsync(cancellationToken);
        }
    }

    internal bool ContainsLogText(string value)
    {
        lock (sync)
        {
            return logPayloads.Any(payload =>
                Encoding.UTF8.GetString(payload).Contains(value, StringComparison.Ordinal)
            );
        }
    }

    internal Task WaitForTraceTextAsync(string value, CancellationToken cancellationToken) =>
        WaitForTraceTextCoreAsync(value, cancellationToken);

    internal bool ContainsTraceText(string value)
    {
        lock (sync)
        {
            return ContainsText(tracePayloads, value);
        }
    }

    internal string LogContext(string value) => Context(logPayloads, value);

    internal string TraceContext(string value) => Context(tracePayloads, value);

    private string Context(List<byte[]> payloads, string value)
    {
        lock (sync)
        {
            foreach (byte[] payload in payloads)
            {
                string text = Encoding.UTF8.GetString(payload);
                int index = text.IndexOf(value, StringComparison.Ordinal);
                if (index >= 0)
                {
                    int start = Math.Max(0, index - 300);
                    return text.Substring(start, Math.Min(text.Length - start, value.Length + 600));
                }
            }

            return string.Empty;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await application.StopAsync();
        await application.DisposeAsync();
    }

    private async Task ReceiveLogsAsync(HttpContext context)
    {
        await using var payload = new MemoryStream();
        await context.Request.Body.CopyToAsync(payload, context.RequestAborted);

        if (payload.Length > 0)
        {
            TaskCompletionSource? expectation;
            lock (sync)
            {
                logPayloads.Add(payload.ToArray());
                expectation = nextLogExport;
                nextLogExport = null;
            }

            expectation?.TrySetResult();
        }

        context.Response.StatusCode = StatusCodes.Status200OK;
    }

    private async Task ReceiveTracesAsync(HttpContext context)
    {
        await using var payload = new MemoryStream();
        await context.Request.Body.CopyToAsync(payload, context.RequestAborted);

        if (payload.Length > 0)
        {
            TaskCompletionSource? expectation;
            lock (sync)
            {
                tracePayloads.Add(payload.ToArray());
                expectation = nextTraceExport;
                nextTraceExport = null;
            }

            expectation?.TrySetResult();
        }

        context.Response.StatusCode = StatusCodes.Status200OK;
    }

    private async Task WaitForTraceTextCoreAsync(string value, CancellationToken cancellationToken)
    {
        while (true)
        {
            Task nextExport;
            lock (sync)
            {
                if (ContainsText(tracePayloads, value))
                {
                    return;
                }

                if (nextTraceExport is not null)
                {
                    throw new InvalidOperationException(
                        "A telemetry export expectation is already pending."
                    );
                }

                nextTraceExport = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously
                );
                nextExport = nextTraceExport.Task;
            }

            await nextExport.WaitAsync(cancellationToken);
        }
    }

    private static bool ContainsText(IEnumerable<byte[]> payloads, string value) =>
        payloads.Any(payload =>
            Encoding.UTF8.GetString(payload).Contains(value, StringComparison.Ordinal)
        );
}
