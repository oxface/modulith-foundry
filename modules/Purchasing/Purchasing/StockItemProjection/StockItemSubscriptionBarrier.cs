namespace ModulithFoundry.Modules.Purchasing.StockItemProjection;

internal sealed class StockItemSubscriptionBarrier
{
    private readonly TaskCompletionSource bound = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );

    internal void Complete() => bound.TrySetResult();

    internal Task WaitAsync(CancellationToken cancellationToken) =>
        bound.Task.WaitAsync(cancellationToken);
}
