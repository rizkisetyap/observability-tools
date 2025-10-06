// ------------------------------------------------------------------------------------
// RatingsApiTests.cs  2025
// Copyright Ahmad Ilman Fadilah. All rights reserved.
// ahmadilmanfadilah@gmail.com,ahmadilmanfadilah@outlook.com
// -----------------------------------------------------------------------------------
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using ratings.Models;

namespace ratings.Tests;

public class RatingsApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetAllRatings_ReturnsOk()
    {
        var res = await _client.GetAsync("/");
        res.EnsureSuccessStatusCode();

        var ratings = await res.Content.ReadFromJsonAsync<List<Rating>>();
        Assert.NotNull(ratings);
        Assert.True(ratings.Count >= 4);
    }

    [Fact]
    public async Task GetRatingsByMovieId_ReturnsOk()
    {
        var res = await _client.GetAsync("/1");
        res.EnsureSuccessStatusCode();

        var ratings = await res.Content.ReadFromJsonAsync<List<Rating>>();
        Assert.NotNull(ratings);
        Assert.All(ratings, r => Assert.Equal(1, r.MovieId));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/health")]
    [InlineData("/swagger")]
    [InlineData("/metrics")]
    public async Task CommonPaths_ReturnsOk(string path)
    {
        var res = await _client.GetAsync(path);
        Assert.True(res.IsSuccessStatusCode || res.StatusCode == System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Database_Seeding_Works()
    {
        var res = await _client.GetAsync("/");
        res.EnsureSuccessStatusCode();

        var ratings = await res.Content.ReadFromJsonAsync<List<Rating>>();
        Assert.Contains(ratings ?? throw new InvalidOperationException(), r => r.Reviewer == "Alice");
    }
}