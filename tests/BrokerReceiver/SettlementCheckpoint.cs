using Rebus.Messages;
using Rebus.Pipeline;

namespace ModulithFoundry.BrokerReceiver;

// Test-only synchronization: the real handler has committed, but Rebus has not settled the delivery.
internal sealed class SettlementCheckpoint(bool pauseAfterCommit) : IIncomingStep
{
    public async Task Process(IncomingStepContext context, Func<Task> next)
    {
        await next();
        string messageId = context.Load<TransportMessage>().Headers[Headers.MessageId];
        if (pauseAfterCommit)
        {
            Console.WriteLine($"committed:{messageId}");
            await Task.Delay(Timeout.InfiniteTimeSpan);
        }
        Console.WriteLine($"handled:{messageId}");
    }
}
