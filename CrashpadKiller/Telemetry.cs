using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace CrashpadKiller;

internal static class Telemetry
{
    internal const string ServiceName = "CrashpadKiller";
    private static readonly ActivitySource ActivitySource = new(ServiceName);
    private static readonly Meter Meter = new(ServiceName);

    private static readonly Counter<long> ConfigLoadAttempts = Meter.CreateCounter<long>("crashpadkiller.config.load.attempts");
    private static readonly Counter<long> ConfigLoadFailures = Meter.CreateCounter<long>("crashpadkiller.config.load.failures");
    private static readonly Histogram<double> ConfigLoadDuration = Meter.CreateHistogram<double>("crashpadkiller.config.load.duration", unit: "s");
    private static readonly Counter<long> ProcessKillAttempts = Meter.CreateCounter<long>("crashpadkiller.process.kill.attempts");
    private static readonly Counter<long> ProcessKillFailures = Meter.CreateCounter<long>("crashpadkiller.process.kill.failures");
    private static readonly Histogram<double> ProcessKillDuration = Meter.CreateHistogram<double>("crashpadkiller.process.kill.duration", unit: "s");
    private static readonly Histogram<double> ServiceIterationDuration = Meter.CreateHistogram<double>("crashpadkiller.service.iteration.duration", unit: "s");

    internal static IDisposable? InitializeIfEnabled()
    {
        if (!IsEnabled())
        {
            return null;
        }

        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(serviceName: ServiceName, serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString());

        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddSource(ServiceName)
            .AddOtlpExporter()
            .Build();

        var meterProvider = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddMeter(ServiceName)
            .AddOtlpExporter()
            .Build();

        return new CompositeDisposable(tracerProvider, meterProvider);
    }

    internal static Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
        => ActivitySource.StartActivity(name, kind);

    internal static void RecordConfigLoadAttempt() => ConfigLoadAttempts.Add(1);

    internal static void RecordConfigLoadFailure(TimeSpan duration)
    {
        ConfigLoadFailures.Add(1);
        ConfigLoadDuration.Record(duration.TotalSeconds);
    }

    internal static void RecordConfigLoadSuccess(TimeSpan duration)
        => ConfigLoadDuration.Record(duration.TotalSeconds);

    internal static void RecordProcessKillAttempt() => ProcessKillAttempts.Add(1);

    internal static void RecordProcessKillFailure() => ProcessKillFailures.Add(1);

    internal static void RecordProcessKillDuration(TimeSpan duration)
        => ProcessKillDuration.Record(duration.TotalSeconds);

    internal static void RecordServiceIterationDuration(TimeSpan duration)
        => ServiceIterationDuration.Record(duration.TotalSeconds);

    private static bool IsEnabled()
    {
        return IsTruthy(Environment.GetEnvironmentVariable("CRASHPADKILLER_OTEL_ENABLED"))
            || HasAnyOpenTelemetryConfiguration();
    }

    private static bool HasAnyOpenTelemetryConfiguration()
    {
        var configurationKeys = new[]
        {
            "OTEL_EXPORTER_OTLP_ENDPOINT",
            "OTEL_EXPORTER_OTLP_TRACES_ENDPOINT",
            "OTEL_EXPORTER_OTLP_METRICS_ENDPOINT",
            "OTEL_EXPORTER_OTLP_PROTOCOL",
            "OTEL_RESOURCE_ATTRIBUTES",
            "OTEL_TRACES_EXPORTER",
            "OTEL_METRICS_EXPORTER",
            "OTEL_EXPORTER_OTLP_HEADERS"
        };

        return configurationKeys.Any(key => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)));
    }

    private static bool IsTruthy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Equals("on", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CompositeDisposable : IDisposable
    {
        private readonly IDisposable[] _disposables;

        public CompositeDisposable(params IDisposable[] disposables)
        {
            _disposables = disposables;
        }

        public void Dispose()
        {
            foreach (var disposable in _disposables)
            {
                disposable.Dispose();
            }
        }
    }
}
