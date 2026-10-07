using Microsoft.Extensions.DependencyInjection;

namespace ModulithFoundry.Samples.Wholesale.EventPersistenceDemo;

internal sealed class DemoClock : TimeProvider
{
    internal DateTimeOffset Now { get; set; } = FixtureHistories.OpenedAt;

    public override DateTimeOffset GetUtcNow() => Now;
}

internal static class ClockConfiguration
{
    internal static IServiceProvider WithClock(this IServiceProvider provider, DateTimeOffset time)
    {
        provider.GetRequiredService<DemoClock>().Now = time;
        return provider;
    }
}
