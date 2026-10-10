using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace ModulithFoundry.Samples.MessagingWorkerDemo;

internal sealed class BrokerSession(IConnection connection, IChannel channel, string queue)
    : IAsyncDisposable
{
    internal IChannel Channel => channel;
    internal string Queue => queue;

    internal static async Task<BrokerSession> OpenAsync(
        IConfiguration configuration,
        bool createQueue,
        CancellationToken cancellation
    )
    {
        string queue =
            configuration["RabbitMQ:Queue"]
            ?? throw new InvalidOperationException("Configure RabbitMQ:Queue.");
        string address =
            configuration["RabbitMQ:Uri"]
            ?? throw new InvalidOperationException("Configure RabbitMQ:Uri.");
        var factory = new ConnectionFactory
        {
            Uri = new Uri(address),
            AutomaticRecoveryEnabled = false,
            ClientProvidedName = "messaging-worker-" + configuration["role"],
        };
        var connection = await factory.CreateConnectionAsync(cancellation);
        try
        {
            var channel = await connection.CreateChannelAsync(
                new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true
                ),
                cancellation
            );
            try
            {
                if (createQueue)
                    await channel.QueueDeclareAsync(
                        queue,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        cancellationToken: cancellation
                    );
                else
                    await channel.QueueDeclarePassiveAsync(queue, cancellation);

                return new(connection, channel, queue);
            }
            catch
            {
                await channel.DisposeAsync();
                throw;
            }
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await channel.DisposeAsync();
        await connection.DisposeAsync();
    }
}
