using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Semantic.Engine.Contracts.Sagas;

namespace Semantic.Engine.Orchestrator.Persistence;

/// <summary>
/// Maps <see cref="AgentSessionState"/> to the <c>agent_sessions</c> table.
/// </summary>
public sealed class AgentSessionStateMap : SagaClassMap<AgentSessionState>
{
    /// <inheritdoc />
    protected override void Configure(EntityTypeBuilder<AgentSessionState> entity, ModelBuilder model)
    {
        entity.ToTable("agent_sessions");

        entity.Property(state => state.CurrentState).HasMaxLength(64).IsRequired();
        entity.Property(state => state.ConversationHistoryJson).IsRequired();
        entity.Property(state => state.LastStateSnapshot);
        entity.Property(state => state.CreatedAtUtc).IsRequired();
        entity.Property(state => state.UpdatedAtUtc).IsRequired();
    }
}
