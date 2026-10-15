using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Semantic.Engine.Orchestrator.Persistence;

/// <summary>
/// Creates the context for <c>dotnet ef</c> design-time tooling using the connection string from the application configuration.
/// </summary>
public sealed class OrchestratorDbContextFactory : IDesignTimeDbContextFactory<OrchestratorDbContext>
{
    /// <inheritdoc />
    public OrchestratorDbContext CreateDbContext(string[] args)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();

        string connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("The connection string 'Postgres' is not configured.");

        DbContextOptionsBuilder<OrchestratorDbContext> builder = new();
        builder.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable(OrchestratorDbContext.MigrationsHistoryTable));
        return new OrchestratorDbContext(builder.Options);
    }
}
