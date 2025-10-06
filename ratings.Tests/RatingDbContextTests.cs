// ------------------------------------------------------------------------------------
// RatingDbContextTests.cs  2025
// Copyright Ahmad Ilman Fadilah. All rights reserved.
// ahmadilmanfadilah@gmail.com,ahmadilmanfadilah@outlook.com
// -----------------------------------------------------------------------------------

using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ratings.Models;

namespace ratings.Tests;

public class RatingDbContextTests
{
    [Fact]
    public async Task EfCoreCommandInterceptor_ShouldInjectDbCommandToActivity()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var activityData = new Dictionary<string, string>();

        using var listener = new ActivityListener();
        listener.ShouldListenTo = source => true;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
        listener.ActivityStarted = activity =>
        {
            activityData.Clear();
            activity.Start();
        };
        listener.ActivityStopped = activity =>
        {
            foreach (var tag in activity.Tags)
            {
                activityData[tag.Key] = tag.Value!;
            }
        };

        ActivitySource.AddActivityListener(listener);

        var options = new DbContextOptionsBuilder<RatingDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setupContext = new RatingDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync();
            setupContext.Ratings.Add(new Rating
            {
                Id = 1,
                MovieId = 1,
                Score = 5
            });
            await setupContext.SaveChangesAsync();
        }

        using var activity = new Activity("test-activity");
        activity.Start();

        await using (var context = new RatingDbContext(options))
        {
            await context.Ratings.ToListAsync();
        }

        activity.Stop();
        Assert.Contains("db.statement", activityData);
        Assert.Contains("SELECT", activityData["db.statement"]);
        Assert.Contains("db.parameters", activityData);
    }

    [Fact]
    public async Task EfCoreCommandInterceptor_ShouldInjectDbCommandToActivity_Async()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var activityData = new Dictionary<string, string?>();

        using var listener = new ActivityListener();
        listener.ShouldListenTo = source => true;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
        listener.ActivityStarted = activity => activity.Start();
        listener.ActivityStopped = activity =>
        {
            foreach (var tag in activity.Tags)
                activityData[tag.Key] = tag.Value;
        };

        ActivitySource.AddActivityListener(listener);

        var options = new DbContextOptionsBuilder<RatingDbContext>()
            .UseSqlite(connection)
            .Options;
        await using (var setupContext = new RatingDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync();
            setupContext.Ratings.Add(new Rating
            {
                Id = 2,
                MovieId = 1,
                Score = 4
            });
            await setupContext.SaveChangesAsync();
        }

        using var activity = new Activity("test-async-activity");
        activity.Start();

        await using (var context = new RatingDbContext(options))
        {
            await context.Ratings.FirstOrDefaultAsync(r => r.MovieId == 1);
        }

        activity.Stop();
        Assert.Contains("db.statement", activityData);
        Assert.Contains("1", activityData["db.statement"]);
    }

    [Fact]
    public void EfCoreCommandInterceptor_ShouldInjectDbCommandToActivity_Sync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var activityData = new Dictionary<string, string?>();

        using var listener = new ActivityListener();
        listener.ShouldListenTo = source => true;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
        listener.ActivityStarted = activity => activity.Start();
        listener.ActivityStopped = activity =>
        {
            foreach (var tag in activity.Tags)
                activityData[tag.Key] = tag.Value;
        };

        ActivitySource.AddActivityListener(listener);

        var options = new DbContextOptionsBuilder<RatingDbContext>()
            .UseSqlite(connection)
            .Options;
        using (var setupContext = new RatingDbContext(options))
        {
            setupContext.Database.EnsureCreated();
            setupContext.Ratings.Add(new Rating
            {
                Id = 3,
                MovieId = 99,
                Score = 3
            });
            setupContext.SaveChanges();
        }

        using var activity = new Activity("test-sync-activity");
        activity.Start();

        using (var context = new RatingDbContext(options))
        {
            _ = context.Ratings.FirstOrDefault(r => r.MovieId == 99);
        }

        activity.Stop();

        Assert.Contains("db.statement", activityData);
        Assert.Contains("99", activityData["db.statement"]);
        Assert.Contains("db.parameters", activityData);
    }

    [Fact]
    public void EfCoreCommandInterceptor_Should_Inject_DbCommand_ToActivity_Sync_ReaderExecuting()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var activityData = new Dictionary<string, string?>();

        using var listener = new ActivityListener();
        listener.ShouldListenTo = _ => true;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
        listener.ActivityStarted = activity => activity.Start();
        listener.ActivityStopped = activity =>
        {
            foreach (var tag in activity.Tags)
                activityData[tag.Key] = tag.Value;
        };

        ActivitySource.AddActivityListener(listener);

        var options = new DbContextOptionsBuilder<RatingDbContext>()
            .UseSqlite(connection)
            .Options;
        using (var setupContext = new RatingDbContext(options))
        {
            setupContext.Database.EnsureCreated();
            setupContext.Ratings.Add(new Rating
            {
                Id = 5,
                MovieId = 500,
                Score = 3
            });
            setupContext.SaveChanges();
        }

        using var activity = new Activity("sync-reader-activity");
        activity.Start();

        using (var context = new RatingDbContext(options))
        {
            var result = context.Ratings.Where(r => r.MovieId == 500).ToList();
            Assert.NotEmpty(result);
        }

        activity.Stop();

        Assert.Contains("db.statement", activityData);
        Assert.Contains("db.parameters", activityData);
        Assert.Contains("500", activityData["db.statement"]);
    }
}