using System.Data.Common;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace movies;

public static class AddHealthCheckHandler
{
    private const string Ready = "ready";
    public static void AddHealthCheck(this IServiceCollection services, ConfigurationManager builderConfiguration)
    {
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: [Ready, "live"])
            .AddSqlite(
                connectionString: "Data Source=movies.db",
                name: "sqlite",
                failureStatus: HealthStatus.Unhealthy,
                tags: [Ready]
            )
            .AddUrlGroup(
                new Uri(builderConfiguration.GetSection("RatingsApi")["BaseUrl"] + "/health/ready"),
                name: "ratings-api",
                tags: [Ready]);
    }

    public static void UseHealthCheck(this IApplicationBuilder app)
    {
        app.UseHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(Ready),
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
    private const string Ready = "ready";
    public static async Task WriteMinimalResponse(HttpContext httpContext, HealthReport report)
    {
        httpContext.Response.ContentType = "application/json";

        var result = new
        {
            status = report.Status == HealthStatus.Healthy ? Ready : "not-ready",
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
internal sealed class TimeSpanConverter : JsonConverter<TimeSpan>
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

public class EfCoreCommandInterceptor : DbCommandInterceptor
{
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        InjectCommandToActivity(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        InjectCommandToActivity(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    private static void InjectCommandToActivity(DbCommand command)
    {
        var activity = Activity.Current;

        if (activity == null)
            return;

        activity.SetTag("db.statement", command.CommandText);
        activity.SetTag("db.parameters", string.Join(", ",
            command.Parameters
                .Cast<DbParameter>()
                .Select(p => $"{p.ParameterName} = {p.Value}")));
    }
}