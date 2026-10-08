using ModulithFoundry.Samples.MessagingDemo;

string sender =
    Environment.GetEnvironmentVariable("MESSAGING_DEMO_SENDER_CONNECTION_STRING")
    ?? throw new InvalidOperationException("Set MESSAGING_DEMO_SENDER_CONNECTION_STRING.");
string receiver =
    Environment.GetEnvironmentVariable("MESSAGING_DEMO_RECEIVER_CONNECTION_STRING")
    ?? throw new InvalidOperationException("Set MESSAGING_DEMO_RECEIVER_CONNECTION_STRING.");
string broker =
    Environment.GetEnvironmentVariable("MESSAGING_DEMO_RABBITMQ")
    ?? throw new InvalidOperationException("Set MESSAGING_DEMO_RABBITMQ.");
await MessagingJourney.RunAsync(
    sender,
    receiver,
    new Uri(broker),
    Console.Out,
    CancellationToken.None
);
