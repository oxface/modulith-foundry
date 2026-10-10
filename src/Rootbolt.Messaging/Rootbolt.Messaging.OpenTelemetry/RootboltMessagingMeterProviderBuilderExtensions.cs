namespace OpenTelemetry.Metrics;

/// <summary>Opt-in collection of Rootbolt messaging measurements by a native OTel provider.</summary>
public static class RootboltMessagingMeterProviderBuilderExtensions
{
    /// <summary>Subscribes to the Rootbolt.Messaging meter.</summary>
    /// <param name="builder">The host's native meter provider builder.</param>
    /// <returns>The supplied builder for further configuration.</returns>
    /// <exception cref="ArgumentNullException">The builder is null.</exception>
    /// <remarks>
    /// Resources, aggregation and exporters remain host-owned.
    /// This does not enable traces or configure logging.
    /// </remarks>
    public static MeterProviderBuilder AddRootboltMessaging(this MeterProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddMeter("Rootbolt.Messaging");
    }
}
