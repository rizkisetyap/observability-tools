using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ratings;

public static class AddHealthCheckHandler
{
    public static void AddHealthCheck(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["ready", "live"])
            .AddSqlite(
                connectionString: "Data Source=ratings.db",
                name: "sqlite",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"]
            );
    }

    public static void UseHealthCheck(this IApplicationBuilder app)
    {
        app.UseHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = UiResponseWriter.WriteMinimalResponse
        });

        app.UseHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live"),
            ResponseWriter = UiResponseWriter.WriteMinimalResponse
        });
    }
}

public static class UiResponseWriter
{
    private static readonly Lazy<JsonSerializerOptions> Options = new(CreateJsonOptions);

    public static async Task WriteMinimalResponse(HttpContext httpContext, HealthReport report)
    {
        httpContext.Response.ContentType = "application/json";

        var result = new
        {
            status = report.Status == HealthStatus.Healthy ? "ready" : "not-ready",
            details = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.Status == HealthStatus.Healthy ? "up" : "down"
            )
        };

        if (report.Status != HealthStatus.Healthy)
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        }

        await JsonSerializer.SerializeAsync(httpContext.Response.Body, result, Options.Value);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };

        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new TimeSpanConverter());

        return options;
    }
}

/// <summary>
/// TimeSpanConverter
/// </summary>
public sealed class TimeSpanConverter : JsonConverter<TimeSpan>
{
    /// <summary>
    /// Read
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="typeToConvert"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public override TimeSpan Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        return default;
    }

    /// <summary>
    /// Write
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="value"></param>
    /// <param name="options"></param>
    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}