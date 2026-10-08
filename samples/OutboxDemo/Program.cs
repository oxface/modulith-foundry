using ModulithFoundry.Samples.OutboxDemo;

string connection =
    Environment.GetEnvironmentVariable("OUTBOX_DEMO_CONNECTION_STRING")
    ?? throw new InvalidOperationException(
        "Set OUTBOX_DEMO_CONNECTION_STRING to a disposable PostgreSQL database."
    );
string receiver =
    Environment.GetEnvironmentVariable("OUTBOX_DEMO_RECEIVER")
    ?? throw new InvalidOperationException(
        "Set OUTBOX_DEMO_RECEIVER to the consumer's HTTP command intake endpoint."
    );
await DemoJourneys.RunAsync(connection, new Uri(receiver), Console.Out, CancellationToken.None);
