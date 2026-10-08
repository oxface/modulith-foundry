using ModulithFoundry.Samples.Wholesale.EventPersistenceDemo;

string connection =
    Environment.GetEnvironmentVariable("WHOLESALE_DEMO_CONNECTION_STRING")
    ?? throw new InvalidOperationException(
        "Set WHOLESALE_DEMO_CONNECTION_STRING to a disposable demo database."
    );
if (args.Contains("--inbox", StringComparer.Ordinal))
{
    string broker =
        Environment.GetEnvironmentVariable("WHOLESALE_DEMO_RABBITMQ")
        ?? throw new InvalidOperationException(
            "Set WHOLESALE_DEMO_RABBITMQ to a disposable RabbitMQ broker URI."
        );
    await InboxJourney.RunAsync(connection, new Uri(broker), Console.Out, CancellationToken.None);
    return;
}
if (args.Contains("--outbox", StringComparer.Ordinal))
{
    string broker =
        Environment.GetEnvironmentVariable("WHOLESALE_DEMO_RABBITMQ")
        ?? throw new InvalidOperationException(
            "Set WHOLESALE_DEMO_RABBITMQ to a disposable RabbitMQ broker URI."
        );
    await OutboxJourney.RunAsync(connection, new Uri(broker), Console.Out, CancellationToken.None);
    return;
}
if (args.Contains("--schema-evolution", StringComparer.Ordinal))
{
    await SchemaEvolutionJourney.RunAsync(
        connection,
        Path.Combine(AppContext.BaseDirectory, "Fixtures"),
        Console.Out,
        CancellationToken.None
    );
    return;
}
await DemoJourneys.RunAsync(
    connection,
    Path.Combine(AppContext.BaseDirectory, "Fixtures"),
    Console.Out,
    CancellationToken.None
);
await AppendJourneys.RunAsync(connection, Console.Out, CancellationToken.None);
await StockIssueJourney.RunAsync(connection, Console.Out, CancellationToken.None);
await RebuildJourney.RunAsync(connection, Console.Out, CancellationToken.None);
