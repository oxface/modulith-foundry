using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

public static class PostgresExtensions
{
    public static TBuilder AddPostgresDataSource<TBuilder>(
        this TBuilder builder,
        string connectionName)
        where TBuilder : IHostApplicationBuilder
    {
        string? connectionString = builder.Configuration.GetConnectionString(connectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{connectionName}' is required.");
        }

        builder.Services.AddNpgsqlDataSource(connectionString);
        builder.Services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>(connectionName);
        builder.Services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddNpgsql());
        builder.Services.ConfigureOpenTelemetryMeterProvider(metrics =>
            metrics.AddNpgsqlInstrumentation());

        return builder;
    }

    private sealed class PostgresHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await dataSource.CreateCommand("SELECT 1")
                    .ExecuteScalarAsync(cancellationToken);

                return HealthCheckResult.Healthy();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                return HealthCheckResult.Unhealthy(
                    "The PostgreSQL database is unavailable.",
                    exception);
            }
        }
    }
}
