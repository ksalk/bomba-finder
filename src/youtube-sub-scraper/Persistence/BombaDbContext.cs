using Microsoft.EntityFrameworkCore;

namespace YoutubeSubScraper.Persistence
{
    internal class BombaDbContext : DbContext
    {
        public DbSet<BombaSubtitles> BombaSubtitles { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source=bomba_{Guid.NewGuid()}.db");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder
                .Entity<BombaSubtitles>()
                .HasKey(x => x.Id);

            base.OnModelCreating(modelBuilder);
        }
    }
}
