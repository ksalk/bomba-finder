using Microsoft.EntityFrameworkCore;

namespace YoutubeSubScraper.Persistence;

public class Persistence
{
    public static async Task<List<string>> GetVideoIdsFromDb(string dbName)
    {
        var options = new DbContextOptionsBuilder().UseSqlite($"Data Source={dbName}");
        await using var db = new BombaDbContext(options);
        await db.Database.EnsureCreatedAsync();

        return await db.BombaSubtitles
            .Select(x => x.VideoId)
            .Distinct()
            .ToListAsync();
    }
    
    public static async Task SaveBombaSubtitlesToDb(List<BombaSubtitles> bombaSubtitles, string dbName)
    {
        var options = new DbContextOptionsBuilder().UseSqlite($"Data Source={dbName}");
        await using var db = new BombaDbContext(options);
        await db.Database.EnsureCreatedAsync();

        await db.BombaSubtitles.AddRangeAsync(bombaSubtitles);
        await db.SaveChangesAsync();
    }
}