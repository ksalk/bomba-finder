using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YoutubeSubScraper
{
    internal class BombaDbContext : DbContext
    {
        public DbSet<BombaSubtitles> BombaSubtitles { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=bomba3.db");
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
