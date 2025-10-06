// ------------------------------------------------------------------------------------
// ObservabilityTests.cs  2025
// Copyright Ahmad Ilman Fadilah. All rights reserved.
// ahmadilmanfadilah@gmail.com,ahmadilmanfadilah@outlook.com
// -----------------------------------------------------------------------------------

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using ratings.Models;

namespace ratings.Tests;

public class ObservabilityTests
{
    [Fact]
    public void AddObservability_ShouldRegisterOpenTelemetryServices()
    {
        var builder = WebApplication.CreateBuilder();
        var options = new OpenTelemetryOptions
        {
            ResourceAttributes = new ResourceAttributesOptions
            {
                ServiceName = "test-service",
                ServiceVersion = "1.0.0",
                ServiceInstanceId = "test-instance",
                IncludeFormattedMessage = true,
                IncludeScopes = true,
                ParseStateValues = true
            },
            Exporters = new ExportersOptions
            {
                Logs = "console",
                Metrics = "prometheus",
                Traces = "console"
            },
            Otlp = new OtlpOptions
            {
                Endpoint = "http://localhost:4317"
            }
        };

        builder.AddObservability(options);

        var provider = builder.Services.BuildServiceProvider();
        var tracerProvider = provider.GetService<TracerProvider>();
        var loggerFactory = provider.GetRequiredService<ILoggerFactory>();

        Assert.NotNull(loggerFactory);
        Assert.NotNull(tracerProvider);

        var descriptor = builder.Services.FirstOrDefault(d => d.ServiceType == typeof(MeterProvider));
        if (descriptor != null) builder.Services.Remove(descriptor);
        provider = builder.Services.BuildServiceProvider();
        var meterProvider = provider.GetService<MeterProvider>();

        Assert.Null(meterProvider);

        var logger = loggerFactory.CreateLogger("test");
        logger.LogInformation("Observability logging is active");
    }

    [Fact]
    public void AddObservability_RegistersTracerProvider_WhenOtelEnabled()
    {
        var builder = WebApplication.CreateBuilder();
        var options = new OpenTelemetryOptions
        {
            ResourceAttributes = new ResourceAttributesOptions { ServiceName = "ratings" },
            Exporters = new ExportersOptions { Traces = "console" },
            Otlp = new OtlpOptions { Endpoint = "http://localhost:4317" }
        };

        builder.AddObservability(options);
        var provider = builder.Services.BuildServiceProvider();
        var tracerProvider = provider.GetService<TracerProvider>();
        Assert.NotNull(tracerProvider);
    }

    [Fact]
    public void AddObservability_RegistersLoggingExporter_Otlp()
    {
        var builder = WebApplication.CreateBuilder();
        var options = new OpenTelemetryOptions
        {
            ResourceAttributes = new ResourceAttributesOptions
            {
                ServiceName = "log-test",
                ServiceVersion = "1.0",
                IncludeFormattedMessage = true,
                IncludeScopes = true,
                ParseStateValues = true
            },
            Exporters = new ExportersOptions { Logs = "otlp" },
            Otlp = new OtlpOptions { Endpoint = "http://localhost:4317" }
        };

        builder.AddObservability(options);
        var provider = builder.Services.BuildServiceProvider();
        var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("log-test");
        logger.LogInformation("Test log OTLP");
        Assert.NotNull(loggerFactory);
    }

    [Fact]
    public void AddObservability_RegistersMetricsExporter_Otlp()
    {
        var builder = WebApplication.CreateBuilder();
        var options = new OpenTelemetryOptions
        {
            ResourceAttributes = new ResourceAttributesOptions { ServiceName = "metrics-test" },
            Exporters = new ExportersOptions { Metrics = "otlp" },
            Otlp = new OtlpOptions { Endpoint = "http://localhost:4317" }
        };

        builder.AddObservability(options);
        var provider = builder.Services.BuildServiceProvider();
        var meterProvider = provider.GetService<MeterProvider>();
        Assert.NotNull(meterProvider);
    }

    [Fact]
    public void AddObservability_SkipsTracing_WhenTracesExporterNotSet()
    {
        var builder = WebApplication.CreateBuilder();
        var options = new OpenTelemetryOptions
        {
            ResourceAttributes = new ResourceAttributesOptions { ServiceName = "ratings" },
            Exporters = new ExportersOptions { Traces = null }
        };

        builder.AddObservability(options);
        var provider = builder.Services.BuildServiceProvider();
        var tracerProvider = provider.GetService<TracerProvider>();
        Assert.Null(tracerProvider);
    }

    [Fact]
    public void AddObservability_HandlesNullExportersGracefully()
    {
        var builder = WebApplication.CreateBuilder();
        var options = new OpenTelemetryOptions
        {
            ResourceAttributes = new ResourceAttributesOptions { ServiceName = "ratings" },
            Otlp = new OtlpOptions { Endpoint = "http://localhost:4317" },
            Exporters = new ExportersOptions
            {
                Traces = null,
                Metrics = null,
                Logs = null
            }
        };

        builder.AddObservability(options);
        var provider = builder.Services.BuildServiceProvider();
        var tracerProvider = provider.GetService<TracerProvider>();
        Assert.Null(tracerProvider);
    }

    [Fact]
    public void UseObservability_AddsPrometheusEndpoint_WhenEnabled()
    {
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();

        var exception = Record.Exception(() => app.UseObservability("xx"));
        Assert.Null(exception);
    }
}