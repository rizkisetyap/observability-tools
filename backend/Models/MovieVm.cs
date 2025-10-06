namespace backend.Models;
public record MovieVm
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string? Genre { get; set; }
    public int Year { get; set; }
    public string Director { get; set; } = string.Empty;
    public List<RatingVm>? Ratings { get; set; } = [];
}