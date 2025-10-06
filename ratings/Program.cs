using System.Diagnostics;
using System.Diagnostics.Metrics;
using ratings;
using Microsoft.EntityFrameworkCore;
using ratings.Models;

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

builder.Services.AddDbContext<RatingDbContext>(options =>
    options.UseSqlite("Data Source=ratings.db"));
builder.AddObservability(otelOptions);
builder.Services.AddHealthCheck();
var app = builder.Build();
app.UseMiddleware<UpstreamAppInfoTracingMiddleware>();

long inProgressRequestCount = 0;

var meter = new Meter(otelOptions?.ResourceAttributes.ServiceName ?? "ratings", otelOptions?.ResourceAttributes.ServiceVersion ?? "1.0.0");
var appUp = meter.CreateUpDownCounter<long>("up", description: "1 if the app is running");
var requestDuration = meter.CreateHistogram<double>(
    name: "http_request_duration",
    unit: "ms",
    description: "HTTP request duration in milliseconds");
var httpStatusCounter = meter.CreateCounter<long>(
    name: "http_requests",
    unit: "requests",
    description: "HTTP responses by status and endpoint");

meter.CreateObservableGauge(
    name: "http_requests_in_progress",
    unit: "requests",
    observeValue: () => new Measurement<long>(Volatile.Read(ref inProgressRequestCount)),
    description: "In-progress HTTP requests");

app.Use(async (context, next) =>
{
    await next();

    var path = NormalizePath(context.Request.Path.Value);
    if (!path.StartsWith("/health") && !path.StartsWith("/metrics") && !path.StartsWith("/swagger"))
    {
        RecordHttpRequest(context.Response.StatusCode.ToString(), path);
    }
});

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
app.Lifetime.ApplicationStarted.Register(async void () =>
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<RatingDbContext>();
    var activitySource = new ActivitySource(otelOptions?.ResourceAttributes.ServiceName ?? "ratings");

    using var rootSpan = activitySource.StartActivity("GenerateInitialRatings");
    rootSpan?.SetTag("db.init", true);
    rootSpan?.SetTag("db.table", "Ratings");
    rootSpan?.SetTag("init.mode", "automatic");

    await db.Database.EnsureCreatedAsync();

    if (!await db.Ratings.AnyAsync())
    {
        var ratings = new List<Rating>
        {
            new() { Id = 1, MovieId = 1, Score = 9, Reviewer = "Alice" },
            new() { Id = 2, MovieId = 1, Score = 8, Reviewer = "Bob" },
            new() { Id = 3, MovieId = 2, Score = 10, Reviewer = "Charlie" },
            new() { Id = 4, MovieId = 3, Score = 7, Reviewer = "Diana" }
        };

        foreach (var rating in ratings)
        {
            using var insertSpan = activitySource.StartActivity("InsertRating");
            insertSpan?.SetTag("movie.id", rating.MovieId);
            insertSpan?.SetTag("reviewer", rating.Reviewer);
            insertSpan?.SetTag("score", rating.Score);

            db.Ratings.Add(rating);
        }

        using var saveSpan = activitySource.StartActivity("CommitInsert");
        await db.SaveChangesAsync();

        rootSpan?.SetTag("init.records_inserted", ratings.Count);
    }
    else
    {
        rootSpan?.SetTag("init.records_inserted", 0);
    }
});


app.MapGet("/", async (RatingDbContext db) =>
    {
        using var activity = Activity.Current?.Source.StartActivity("FetchRatingsFromDb");
        var ratings = await db.Ratings.ToListAsync();
        return Results.Ok(ratings);
    }).WithName("GetRatings")
    .WithOpenApi();

app.MapGet("/{movieId:int}", async (int movieId, RatingDbContext db) =>
    {
        using var activity = Activity.Current?.Source.StartActivity("FetchRatingsByMovieIdFromDb");
        var ratings = await db.Ratings
            .Where(r => r.MovieId == movieId)
            .ToListAsync();
        return Results.Ok(ratings);
    }).WithName("GetRatingByMovieId")
    .WithOpenApi();

appUp.Add(1);
app.UseObservability(otelOptions?.Exporters.Metrics);
app.UseHealthCheck();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json",
        $"{otelOptions?.ResourceAttributes.ServiceName ?? "unknown"} {otelOptions?.ResourceAttributes.ServiceVersion ?? "unknown"}");
});
await app.RunAsync();
return;

string NormalizePath(string? path)
{
    if (string.IsNullOrWhiteSpace(path)) return "/";
    var trimmed = path.TrimEnd('/');
    return string.IsNullOrEmpty(trimmed) ? "/" : trimmed;
}

void RecordHttpRequest(string status, string endpoint)
{
    httpStatusCounter.Add(1,
        KeyValuePair.Create<string, object?>("status", status),
        KeyValuePair.Create<string, object?>("endpoint", endpoint));
}
public partial class Program { }