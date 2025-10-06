// ------------------------------------------------------------------------------------
// MoviesApiClient.cs  2025
// Copyright Ahmad Ilman Fadilah. All rights reserved.
// ahmadilmanfadilah@gmail.com,ahmadilmanfadilah@outlook.com
// -----------------------------------------------------------------------------------

using System.Text.Json;
using backend.Models;

namespace backend;


public class MoviesApiClient : IMoviesApiClient
{
    private readonly HttpClient _httpClient;

    public MoviesApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<MovieVm>> GetAllAsync()
    {
        var response = await _httpClient.GetAsync("");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<List<MovieVm>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return result ?? new List<MovieVm>();
    }

    public async Task<MovieVm?> GetByIdAsync(string id)
    {
        var response = await _httpClient.GetAsync($"{id}");
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<MovieVm>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    public async Task<MovieVm?> GetRatingsByMovieIdAsync(string id)
    {
        var response = await _httpClient.GetAsync($"{id}/ratings");
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<MovieVm>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }
}