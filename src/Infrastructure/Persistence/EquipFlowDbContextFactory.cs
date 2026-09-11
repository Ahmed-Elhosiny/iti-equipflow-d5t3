using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pgvector.EntityFrameworkCore;

namespace EquipFlow.Infrastructure.Persistence;

public sealed class EquipFlowDbContextFactory : IDesignTimeDbContextFactory<EquipFlowDbContext>
{
    public EquipFlowDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Database connection string is not configured.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<EquipFlowDbContext>();
        optionsBuilder
            .UsePgVector()
            .UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions.UseVector());

        return new EquipFlowDbContext(optionsBuilder.Options);
    }
}

internal static class PgVectorOptionsBuilderExtensions
{
    public static DbContextOptionsBuilder<TContext> UsePgVector<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder)
        where TContext : DbContext => optionsBuilder;
}
