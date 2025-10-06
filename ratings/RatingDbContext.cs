// ------------------------------------------------------------------------------------
// RatingDbContext.cs  2025
// Copyright Ahmad Ilman Fadilah. All rights reserved.
// ahmadilmanfadilah@gmail.com,ahmadilmanfadilah@outlook.com
// -----------------------------------------------------------------------------------

using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ratings.Models;

namespace ratings;

public class RatingDbContext(DbContextOptions<RatingDbContext> options) : DbContext(options)
{
    public DbSet<Rating> Ratings => Set<Rating>();
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // ⚠️ disable in production
        optionsBuilder.AddInterceptors(new EfCoreCommandInterceptor());
    }
}
public class EfCoreCommandInterceptor : DbCommandInterceptor
{

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        InjectCommandToActivity(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        InjectCommandToActivity(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    private static void InjectCommandToActivity(DbCommand command)
    {
        var activity = Activity.Current;

        if (activity == null)
            return;

        activity.SetTag("db.statement", command.CommandText);
        activity.SetTag("db.parameters", string.Join(", ",
            command.Parameters
                .Cast<DbParameter>()
                .Select(p => $"{p.ParameterName} = {p.Value}")));
    }
}