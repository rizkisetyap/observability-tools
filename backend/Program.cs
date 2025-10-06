using System.Diagnostics;
using System.Diagnostics.Metrics;
using backend;
using backend.Models;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", true, true);
builder
    .Configuration
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", true, false);
builder.Configuration.AddJsonFile("appsettings.Local.json", true, true);
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(args);
builder.Services.Configure<OpenTelemetryOptions>(
    builder.Configuration.GetSection("OpenTelemetry"));
var otelOptions = builder.Configuration
    .GetSection("OpenTelemetry")
    .Get<OpenTelemetryOptions>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<TracingHeaderHandler>();
builder.Services.Configure<MoviesApiOptions>(builder.Configuration.GetSection("MoviesApi"));
builder.Services.AddHttpClient<IMoviesApiClient, MoviesApiClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<MoviesApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
}).AddHttpMessageHandler<TracingHeaderHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = otelOptions?.ResourceAttributes.ServiceName,
        Version = otelOptions?.ResourceAttributes.ServiceName ?? "unknown",
        Description = otelOptions?.ResourceAttributes.ServiceName
    });
});
builder.AddObservability(otelOptions);
builder.Services.AddHealthCheck(builder.Configuration);
var app = builder.Build();
app.UseMiddleware<UpstreamAppInfoTracingMiddleware>();

app.UseRouting();

long inProgressRequestCount = 0;

var meter = new Meter(otelOptions?.ResourceAttributes.ServiceName ?? "backend", otelOptions?.ResourceAttributes.ServiceVersion ?? "1.0.0");
var appUp = meter.CreateUpDownCounter<long>("up", description: "1 if the app is running");

var loginCounter = meter.CreateCounter<long>(
    name: "http_logins",
    unit: "logins",
    description: "Number of successful logins");

var httpStatusCounter = meter.CreateCounter<long>(
    name: "http_requests",
    unit: "requests",
    description: "HTTP responses by status and endpoint");

meter.CreateObservableGauge(
    name: "http_requests_in_progress",
    unit: "requests",
    observeValue: () => new Measurement<long>(Volatile.Read(ref inProgressRequestCount)),
    description: "In-progress HTTP requests");

var requestDuration = meter.CreateHistogram<double>(
    name: "http_request_duration",
    unit: "ms",
    description: "HTTP request duration in milliseconds");

// Middleware to track duration and in-progress count
app.Use(async (context, next) =>
{
    var rawPath = context.Request.Path.Value ?? "/";
    var path = NormalizePath(rawPath);
    if (path.StartsWith("/health") || path.StartsWith("/metrics") || path.StartsWith("/swagger"))
    {
        await next();
        return;
    }

    Interlocked.Increment(ref inProgressRequestCount);
    var sw = Stopwatch.StartNew();

    try
    {
        await next();
    }
    finally
    {
        sw.Stop();
        Interlocked.Decrement(ref inProgressRequestCount);

        requestDuration.Record(sw.Elapsed.TotalMilliseconds,
            KeyValuePair.Create<string, object?>("endpoint", path));
    }
});

// Routes
app.MapGet("/", async () =>
{
    RecordHttpRequest("200", "/");
    var random = new Random();
    var randomNumber = random.Next(0, 3001);
    await Task.Delay(randomNumber); // Simulate slow response
    return Results.Ok("Hello World!");
});

app.MapGet("/login", () =>
{
    loginCounter.Add(1);
    RecordHttpRequest("200", "/login");
    return Results.Ok("Login successful!");
});
app.MapGet("/error", () =>
{
    RecordHttpRequest("500", "/error");
    return Results.StatusCode(500);
});
app.MapGet("/movies", async (IMoviesApiClient moviesClient) =>
{
    try
    {
        var data = await moviesClient.GetAllAsync();
        RecordHttpRequest("200", "/movies");
        return Results.Json(data);
    }
    catch (Exception)
    {
        RecordHttpRequest("500", "/movies");
        return Results.StatusCode(500);
    }
});
const string moviessEndpoint = "/movies/{id}";
app.MapGet(moviessEndpoint, async (string id, IMoviesApiClient moviesClient) =>
{
    try
    {
        var movie = await moviesClient.GetByIdAsync(id);
        if (movie is null)
        {
            RecordHttpRequest("404", moviessEndpoint);
            return Results.NotFound();
        }

        RecordHttpRequest("200", moviessEndpoint);
        return Results.Json(movie);
    }
    catch (Exception)
    {
        RecordHttpRequest("500", moviessEndpoint);
        return Results.StatusCode(500);
    }
});
const string ratingsEndpoint = "/movies/{id}/ratings";
app.MapGet(ratingsEndpoint, async (string id, IMoviesApiClient moviesClient) =>
{
    try
    {
        var movie = await moviesClient.GetRatingsByMovieIdAsync(id);
        if (movie is null)
        {
            RecordHttpRequest("404", ratingsEndpoint);
            return Results.NotFound();
        }

        RecordHttpRequest("200", ratingsEndpoint);
        return Results.Json(movie);
    }
    catch (Exception)
    {
        RecordHttpRequest("500", ratingsEndpoint);
        return Results.StatusCode(500);
    }
});

appUp.Add(1);
app.UseObservability(otelOptions?.Exporters.Metrics);
app.UseHealthCheck();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json",
        $"{otelOptions?.ResourceAttributes.ServiceName} {otelOptions?.ResourceAttributes.ServiceVersion ?? "unknown"}");
});
await app.RunAsync();
return;

void RecordHttpRequest(string status, string endpoint)
{
    httpStatusCounter.Add(1,
        KeyValuePair.Create<string, object?>("status", status),
        KeyValuePair.Create<string, object?>("endpoint", endpoint));
}

string NormalizePath(string? path)
{
    if (string.IsNullOrWhiteSpace(path)) return "/";
    var trimmed = path.TrimEnd('/');
    return string.IsNullOrEmpty(trimmed) ? "/" : trimmed;
}