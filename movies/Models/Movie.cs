// ------------------------------------------------------------------------------------
// Movie.cs  2025
// Copyright Ahmad Ilman Fadilah. All rights reserved.
// ahmadilmanfadilah@gmail.com,ahmadilmanfadilah@outlook.com
// -----------------------------------------------------------------------------------

namespace movies.Models;

public record Movie
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string? Genre { get; set; }
    public int Year { get; set; }
    public string Director { get; set; } = string.Empty;
    public List<RatingVm>? Ratings { get; set; } = [];
}