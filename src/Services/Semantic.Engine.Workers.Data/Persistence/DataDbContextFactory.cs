using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Semantic.Engine.Workers.Data.Persistence;

/// <summary>
/// Creates the context for <c>dotnet ef</c> design-time tooling using the connection string from the application configuration.
/// </summary>
public sealed class DataDbContextFactory : IDesignTimeDbContextFactory<DataDbContext>
{
    /// <inheritdoc />
    public DataDbContext CreateDbContext(string[] args)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();

        string connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("The connection string 'Postgres' is not configured.");

        DbContextOptionsBuilder<DataDbContext> builder = new();
        builder.UseNpgsql(
            connectionString,
            npgsql =>
            {
                npgsql.UseVector();
                npgsql.MigrationsHistoryTable(DataDbContext.MigrationsHistoryTable);
            });
        return new DataDbContext(builder.Options);
    }
}
