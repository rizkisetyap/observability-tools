using movies.Models;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace movies;

public static class AddObservabilityHandler
{
    public static void AddObservability(this WebApplicationBuilder builder, OpenTelemetryOptions? otelOptions)
    {
        if (otelOptions?.Exporters.Metrics is null or "none" && otelOptions?.Exporters.Traces is null or "none" &&
            otelOptions?.Exporters.Logs is null or "none")
        {
            return;
        }

        builder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(
            builder.Configuration.GetSection("AspNetCoreInstrumentation"));

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource =>
                resource
                    .AddService(otelOptions.ResourceAttributes.ServiceName,
                        serviceNamespace: otelOptions.ResourceAttributes.NameSpace,
                        serviceVersion: otelOptions.ResourceAttributes.ServiceVersion ?? "unknown",
                        autoGenerateServiceInstanceId: false,
                        serviceInstanceId: otelOptions.ResourceAttributes.ServiceInstanceId))
            .WithTracing(tracing =>
            {
                if (otelOptions.Exporters.Traces is null or "none") return;
                tracing
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.Filter = httpContext =>
                        {
                            var path = httpContext.Request.Path.Value ?? "";
                            return !path.StartsWith("/health") && !path.StartsWith("/metrics") &&
                                   !path.StartsWith("/swagger");
                        };
                    })
                    .AddHttpClientInstrumentation();
                builder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(
                    builder.Configuration.GetSection("AspNetCoreInstrumentation"));


                if (otelOptions.Exporters.Traces == "otlp")
                {
                    tracing.AddOtlpExporter(opt =>
                    {
                        opt.Endpoint = new Uri(otelOptions.Otlp.Endpoint);
                        opt.Protocol = OtlpExportProtocol.Grpc;
                    });
                }
                else
                {
                    tracing.AddConsoleExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                if (otelOptions.Exporters.Metrics is null or "none") return;
                metrics.AddProcessInstrumentation()
                    // .AddAspNetCoreInstrumentation() jika membutuhkan auto instrument untuk metrics
                    .AddMeter(otelOptions.ResourceAttributes.ServiceName)
                    //eg: "MyApp.ServiceName" atau "Company.Product.Module"
                    // "orders.api", "billing.service" atau "inventory.processor"
                    //  "http.metrics" jika hanya mencatat metrik HTTP internal buatan sendiri.
                    .SetExemplarFilter(ExemplarFilterType.TraceBased);

                switch (otelOptions.Exporters.Metrics)
                {
                    case "prometheus":
                        metrics.AddPrometheusExporter();
                        break;
                    case "otlp":
                        metrics.AddOtlpExporter(opt =>
                        {
                            opt.Endpoint = new Uri(otelOptions.Otlp.Endpoint);
                            opt.Protocol = OtlpExportProtocol.Grpc;
                        });
                        break;
                }
            })
            .WithLogging(logging =>
            {
                if (otelOptions.Exporters.Logs is null or "none" or not "console") return;
                //   Note: ConsoleExporter is used for demo purpose only. In production
                // environment, ConsoleExporter should be replaced with other exporters
                // (e.g. OTLP Exporter).
                //   https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/docs/logs/README.md
                builder.Logging.ClearProviders();
                logging.AddConsoleExporter();
                logging.SetResourceBuilder(ResourceBuilder.CreateDefault()
                    .AddService(otelOptions.ResourceAttributes.ServiceName,
                        serviceNamespace: otelOptions.ResourceAttributes.NameSpace,
                        serviceVersion: otelOptions.ResourceAttributes.ServiceVersion ?? "unknown",
                        autoGenerateServiceInstanceId: false,
                        serviceInstanceId: otelOptions.ResourceAttributes.ServiceInstanceId));
            });
        if (otelOptions.Exporters.Logs is null or "none" or not "otlp") return;
        builder.Logging.ClearProviders();
        builder.Logging.AddOpenTelemetry(log =>
        {
            log.IncludeFormattedMessage = otelOptions.ResourceAttributes.IncludeFormattedMessage;
            log.IncludeScopes = otelOptions.ResourceAttributes.IncludeScopes;
            log.ParseStateValues = otelOptions.ResourceAttributes.ParseStateValues;
            log.SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService(otelOptions.ResourceAttributes.ServiceName,
                    serviceVersion: otelOptions.ResourceAttributes.ServiceVersion ?? "unknown",
                    serviceNamespace: otelOptions.ResourceAttributes.NameSpace,
                    autoGenerateServiceInstanceId: false,
                    serviceInstanceId: otelOptions.ResourceAttributes.ServiceInstanceId));

            log.AddOtlpExporter(opt =>
            {
                opt.Endpoint = new Uri(otelOptions.Otlp.Endpoint);
                opt.Protocol = OtlpExportProtocol.Grpc;
            });
        });
    }

    public static void UseObservability(this IApplicationBuilder app, string? exporters)
    {
        if (exporters == "prometheus")
        {
            app.UseOpenTelemetryPrometheusScrapingEndpoint();
        }
    }
}