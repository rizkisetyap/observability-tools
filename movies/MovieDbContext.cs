// ------------------------------------------------------------------------------------
// MovieDbContext.cs  2025
// Copyright Ahmad Ilman Fadilah. All rights reserved.
// ahmadilmanfadilah@gmail.com,ahmadilmanfadilah@outlook.com
// -----------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using movies.Models;

namespace movies;
class MovieDbContext(DbContextOptions<MovieDbContext> options) : DbContext(options)
{
    public DbSet<Movie> Movies => Set<Movie>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // ⚠️ disable in production
        optionsBuilder.AddInterceptors(new EfCoreCommandInterceptor());
    }
}