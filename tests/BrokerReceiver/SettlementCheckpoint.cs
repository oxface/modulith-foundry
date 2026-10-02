using Rebus.Messages;
using Rebus.Pipeline;

namespace ModulithFoundry.BrokerReceiver;

// Test-only synchronization: the real handler has committed, but Rebus has not settled the delivery.
internal sealed class SettlementCheckpoint(bool pauseAfterCommit, CancellationToken stopping)
    : IIncomingStep
{
    public async Task Process(IncomingStepContext context, Func<Task> next)
    {
        string messageId = context.Load<TransportMessage>().Headers[Headers.MessageId];
        Console.WriteLine($"dispatch:{messageId}");
        try
        {
            await next();
        }
        catch (Exception)
        {
            // Private coordination only: never print payloads or exception text.
            Console.WriteLine($"failed:{messageId}");
            throw;
        }
        if (pauseAfterCommit)
        {
            Console.WriteLine($"committed:{messageId}");
            await Task.Delay(Timeout.InfiniteTimeSpan, stopping);
        }
        Console.WriteLine($"handled:{messageId}");
    }
}
