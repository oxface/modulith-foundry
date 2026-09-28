using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ModulithFoundry.Modules.Access.Invitations.Email;

internal sealed partial class InvitationEmailDeliveryWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<InvitationEmailDeliveryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                bool foundWork = await scope.ServiceProvider
                    .GetRequiredService<InvitationEmailDispatcher>()
                    .DispatchNextAsync(stoppingToken);
                if (!foundWork)
                {
                    await Task.Delay(IdleDelay, timeProvider, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                LogDeliveryLoopFailure(logger, exception);
                try
                {
                    await Task.Delay(IdleDelay, timeProvider, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }
    }

    [LoggerMessage(LogLevel.Error, "The invitation email delivery loop failed.")]
    private static partial void LogDeliveryLoopFailure(
        ILogger logger,
        Exception exception);
}
