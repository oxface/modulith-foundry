using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModulithFoundry.Modules.Purchasing.Contracts;

namespace ModulithFoundry.Modules.Purchasing.StockItemProjection;

internal sealed partial class StockItemBootstrapWorker(
    IServiceScopeFactory scopes,
    ILogger<StockItemBootstrapWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using AsyncServiceScope scope = scopes.CreateAsyncScope();
                await scope
                    .ServiceProvider.GetRequiredService<IStockItemProjectionBootstrapper>()
                    .EnsureInitializedAsync(stoppingToken);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                BootstrapFailed(logger, exception);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Purchasing Stock Item bootstrap failed; retrying in a fresh scope."
    )]
    private static partial void BootstrapFailed(ILogger logger, Exception exception);
}
