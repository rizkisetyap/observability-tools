// ------------------------------------------------------------------------------------
// Rating.cs  2025
// Copyright Ahmad Ilman Fadilah. All rights reserved.
// ahmadilmanfadilah@gmail.com,ahmadilmanfadilah@outlook.com
// -----------------------------------------------------------------------------------

namespace ratings.Models;

public record Rating
{
    public int Id { get; set; }
    public int MovieId { get; set; }
    public int Score { get; set; } // 1 - 10
    public string Reviewer { get; set; } = string.Empty;
}
