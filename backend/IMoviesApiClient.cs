// ------------------------------------------------------------------------------------
// IMoviesApiClient.cs  2025
// Copyright Ahmad Ilman Fadilah. All rights reserved.
// ahmadilmanfadilah@gmail.com,ahmadilmanfadilah@outlook.com
// -----------------------------------------------------------------------------------

using backend.Models;

namespace backend;

public interface IMoviesApiClient
{
    Task<List<MovieVm>> GetAllAsync();
    Task<MovieVm?> GetByIdAsync(string id);

    Task<MovieVm?> GetRatingsByMovieIdAsync(string id);
}