namespace OpenTelemetry.Trace;

/// <summary>Opt-in collection of Rootbolt messaging activities by a native OTel provider.</summary>
public static class RootboltMessagingTracerProviderBuilderExtensions
{
    /// <summary>Subscribes to the Rootbolt.Messaging activity source.</summary>
    /// <param name="builder">The host's native tracer provider builder.</param>
    /// <returns>The supplied builder for further configuration.</returns>
    /// <exception cref="ArgumentNullException">The builder is null.</exception>
    /// <remarks>
    /// Sampling, resources, exporters and transport instrumentation remain host-owned.
    /// This does not enable metrics or configure logging.
    /// </remarks>
    public static TracerProviderBuilder AddRootboltMessaging(this TracerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddSource("Rootbolt.Messaging");
    }
}
