using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;

namespace Semantic.Engine.Orchestrator.Persistence;

/// <summary>
/// Entity Framework Core context that stores the saga state of agent sessions in PostgreSQL.
/// </summary>
public sealed class OrchestratorDbContext(DbContextOptions<OrchestratorDbContext> options) : SagaDbContext(options)
{
    /// <summary>The name of the migrations history table that keeps this context isolated from other contexts.</summary>
    public const string MigrationsHistoryTable = "__orchestrator_migrations";

    /// <inheritdoc />
    protected override IEnumerable<ISagaClassMap> Configurations
    {
        get { yield return new AgentSessionStateMap(); }
    }
}
