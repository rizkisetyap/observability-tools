// ------------------------------------------------------------------------------------
// RatingsApiClient.cs  2025
// Copyright Ahmad Ilman Fadilah. All rights reserved.
// ahmadilmanfadilah@gmail.com,ahmadilmanfadilah@outlook.com
// -----------------------------------------------------------------------------------

using System.Text.Json;
using movies.Models;

namespace movies;

public interface IRatingsApiClient
{
    Task<List<RatingVm>?> GetByMovieIdAsync(int movieId);
}

public class RatingsApiClient : IRatingsApiClient
{
    private readonly HttpClient _httpClient;

    public RatingsApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<RatingVm>?> GetByMovieIdAsync(int movieId)
    {
        var response = await _httpClient.GetAsync($"{movieId}");
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<RatingVm>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }
}