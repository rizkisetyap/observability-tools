using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using movies;
using movies.Models;

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
builder.Services.Configure<RatingsApiOptions>(builder.Configuration.GetSection("RatingsApi"));
builder.Services.AddHttpClient<IRatingsApiClient, RatingsApiClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<RatingsApiOptions>>().Value;
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
builder.Services.AddDbContext<MovieDbContext>(options =>
    options.UseSqlite("Data Source=movies.db"));
builder.AddObservability(otelOptions);
builder.Services.AddHealthCheck(builder.Configuration);
var app = builder.Build();
app.UseMiddleware<UpstreamAppInfoTracingMiddleware>();

long inProgressRequestCount = 0;

var meter = new Meter(otelOptions?.ResourceAttributes.ServiceName ?? "movies",
    otelOptions?.ResourceAttributes.ServiceVersion ?? "1.0.0");
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
// await Task.Delay(5000); tracing initial insert not working when jager start same time as app in docker compose, so we need to wait for the jaeger to be created
// DB Initialization
app.Lifetime.ApplicationStarted.Register(async () =>
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<MovieDbContext>();
        var activitySource = new ActivitySource(otelOptions?.ResourceAttributes.ServiceName ?? "movies");

        using var rootSpan = activitySource.StartActivity("GenerateInitialMovies");
        rootSpan?.SetTag("db.init", true);
        rootSpan?.SetTag("db.table", "Movies");

        await db.Database.EnsureCreatedAsync();

        if (!await db.Movies.AnyAsync())
        {
            var movies = new List<Movie>
            {
                new() { Id = 1, Title = "Inception", Genre = "Sci-Fi", Year = 2010, Director = "Christopher Nolan" },
                new() { Id = 2, Title = "The Matrix", Genre = "Action", Year = 1999, Director = "The Wachowskis" },
                new() { Id = 3, Title = "Interstellar", Genre = "Sci-Fi", Year = 2014, Director = "Christopher Nolan" },
                new()
                {
                    Id = 4, Title = "The Godfather", Genre = "Crime", Year = 1972, Director = "Francis Ford Coppola"
                },
                new() { Id = 5, Title = "Parasite", Genre = "Thriller", Year = 2019, Director = "Bong Joon-ho" }
            };

            foreach (var movie in movies)
            {
                using var insertSpan = activitySource.StartActivity("InsertMovie");
                insertSpan?.SetTag("title", movie.Title);
                insertSpan?.SetTag("genre", movie.Genre);
                insertSpan?.SetTag("year", movie.Year);
                insertSpan?.SetTag("director", movie.Director);

                db.Movies.Add(movie);
            }

            using var saveSpan = activitySource.StartActivity("CommitInsert");
            await db.SaveChangesAsync();

            rootSpan?.SetTag("init.records_inserted", movies.Count);
            rootSpan?.SetTag("init.mode", "automatic");
        }
        else
        {
            rootSpan?.SetTag("init.records_inserted", 0);
        }
    }
});

app.MapGet("/", async (MovieDbContext db) =>
    {
        using var activity = Activity.Current?.Source.StartActivity("FetchMoviesFromDb");
        var movies = await db.Movies.ToListAsync();
        return Results.Ok(movies);
    }).WithName("GetMovies")
    .WithOpenApi();

app.MapGet("/{id:int}", async (int id, MovieDbContext db) =>
    {
        using var activity = Activity.Current?.Source.StartActivity("GetMovieById");
        activity?.SetTag("movie.id", id);
        var movie = await db.Movies.FindAsync(id);
        if (movie is not null) return Results.Ok(movie);
        activity?.SetTag("error", true);
        activity?.SetTag("message", "Movie not found");
        return Results.NotFound();
    }).WithName("GetMovieById")
    .WithOpenApi();

app.MapGet("/{movieId:int}/ratings",
        async (int movieId, MovieDbContext db, IRatingsApiClient ratingsApiClient) =>
        {
            using var activity = Activity.Current?.Source.StartActivity("GetMovie");
            activity?.SetTag("movie.id", movieId);

            var movie = await db.Movies.FindAsync(movieId);
            if (movie is null)
            {
                activity?.SetTag("error", true);
                activity?.SetTag("message", "Movie not found");
                return Results.NotFound();
            }

            using var activity2 = Activity.Current?.Source.StartActivity("GetRatingsByMovieId");
            activity2?.SetTag("movie.id", movieId);
            var ratings = await ratingsApiClient.GetByMovieIdAsync(movieId);
            if (ratings == null)
            {
                activity2?.SetTag("ratings.notfound", true);
            }
            movie.Ratings = ratings;
            return Results.Ok(movie);
        }).WithName("GetMovieWithRatings")
    .WithOpenApi();
appUp.Add(1);
app.UseObservability(otelOptions?.Exporters.Metrics);
app.UseHealthCheck();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json",
        $"{otelOptions?.ResourceAttributes.ServiceName ?? "unknown"} {otelOptions?.ResourceAttributes.ServiceVersion}");
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