using Microsoft.EntityFrameworkCore;

namespace YoutubeSubScraper.Persistence
{
    internal class BombaDbContext : DbContext
    {
        public DbSet<BombaSubtitles> BombaSubtitles { get; set; }

        public BombaDbContext(DbContextOptionsBuilder optionsBuilder) : base(optionsBuilder.Options) { }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder
                .Entity<BombaSubtitles>()
                .HasKey(x => x.Id);

            base.OnModelCreating(modelBuilder);
        }
    }
}
