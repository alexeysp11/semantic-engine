using Microsoft.EntityFrameworkCore;
using Semantic.Engine.Workers.Data.Options;

namespace Semantic.Engine.Workers.Data.Persistence;

/// <summary>
/// Entity Framework Core context of the vector store.
/// </summary>
public sealed class DataDbContext(DbContextOptions<DataDbContext> options) : DbContext(options)
{
    /// <summary>The name of the migrations history table that keeps this context isolated from other contexts.</summary>
    public const string MigrationsHistoryTable = "__data_migrations";

    /// <summary>Gets the embedded text chunks.</summary>
    public DbSet<MemoryChunk> MemoryChunks => Set<MemoryChunk>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Installs the pgvector extension through the migration script
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<MemoryChunk>(entity =>
        {
            entity.ToTable("memory_chunks");
            entity.HasKey(chunk => chunk.Id);
            entity.Property(chunk => chunk.Source).HasMaxLength(128).IsRequired();
            entity.Property(chunk => chunk.Content).IsRequired();
            entity.Property(chunk => chunk.Embedding)
                .HasColumnType($"vector({DataWorkerOptions.EmbeddingDimensions})")
                .IsRequired();

            entity.HasIndex(chunk => chunk.SessionId);
            entity.HasIndex(chunk => chunk.Embedding)
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops");
        });
    }
}
