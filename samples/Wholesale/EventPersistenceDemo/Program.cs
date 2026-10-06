using ModulithFoundry.Samples.Wholesale.EventPersistenceDemo;

string connection =
    Environment.GetEnvironmentVariable("WHOLESALE_DEMO_CONNECTION_STRING")
    ?? throw new InvalidOperationException(
        "Set WHOLESALE_DEMO_CONNECTION_STRING to a disposable demo database."
    );
await DemoJourneys.RunAsync(
    connection,
    Path.Combine(AppContext.BaseDirectory, "Fixtures"),
    Console.Out,
    CancellationToken.None
);
