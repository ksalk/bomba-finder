using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Bomba.DB;

public class BombaDbContext : DbContext
{
    public DbSet<VideoScript> VideoScripts { get; set; }
    public DbSet<ScriptChunk> ScriptChunks { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING")
            ?? "Host=localhost;Database=bomba_db;Username=postgres;Password=yourpassword";

        optionsBuilder.UseNpgsql(connectionString, options => options.UseVector());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.HasPostgresExtension("pg_trgm");

        modelBuilder.Entity<VideoScript>()
            .HasKey(v => v.Id);

        modelBuilder.Entity<VideoScript>()
            .Property(v => v.Segments)
            .HasConversion(
                v => JsonSerializer.Serialize(v),
                v => JsonSerializer.Deserialize<List<ScriptSegment>>(v));

        modelBuilder.Entity<VideoScript>()
            .HasMany(v => v.Chunks)
            .WithOne(c => c.VideoScript)
            .HasForeignKey(c => c.VideoScriptId);


        modelBuilder.Entity<ScriptChunk>()
            .HasKey(s => s.Id);

        modelBuilder.Entity<ScriptChunk>()
            .Property(s => s.Embedding)
            .HasColumnType("vector(3072)");

        modelBuilder.Entity<ScriptChunk>()
            .HasIndex(e => e.NormalizedText)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");
    }
}
