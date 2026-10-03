using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace ModulithFoundry.TopologyTests;

public sealed partial class LocalRuntimeTests
{
    private static async Task WaitForApiReplicaExitAsync(
        DistributedApplication app,
        string replicaId,
        CancellationToken cancellationToken
    )
    {
        if (
            app.ResourceNotifications.TryGetCurrentState(replicaId, out var current)
            && (
                current.Snapshot.State?.Text == KnownResourceStates.Exited
                || current.Snapshot.State?.Text == KnownResourceStates.Finished
            )
        )
            return;
        await foreach (var update in app.ResourceNotifications.WatchAsync(cancellationToken))
        {
            if (
                update.ResourceId == replicaId
                && (
                    update.Snapshot.State?.Text == KnownResourceStates.Exited
                    || update.Snapshot.State?.Text == KnownResourceStates.Finished
                )
            )
                return;
        }
        throw new InvalidOperationException(
            "API replica observation ended before shutdown completed."
        );
    }

    private static async Task<string[]> WaitForApiReplicasAsync(
        DistributedApplication app,
        int expected,
        CancellationToken cancellationToken
    )
    {
        var running = new HashSet<string>(StringComparer.Ordinal);
        await foreach (var update in app.ResourceNotifications.WatchAsync(cancellationToken))
        {
            if (update.Resource.Name != "api")
                continue;
            if (update.Snapshot.State?.Text == KnownResourceStates.Running)
                running.Add(update.ResourceId);
            else
                running.Remove(update.ResourceId);
            if (running.Count == expected)
                return running.ToArray();
        }
        throw new InvalidOperationException(
            "API replica observation ended before startup completed."
        );
    }
}
