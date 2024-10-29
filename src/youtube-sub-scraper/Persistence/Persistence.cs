namespace YoutubeSubScraper.Persistence;

public class Persistence
{
    public static async Task SaveBombaSubtitlesToDb(List<BombaSubtitles> bombaSubtitles)
    {
        await using var db = new BombaDbContext();
        await db.Database.EnsureCreatedAsync();

        foreach (var subtitle in bombaSubtitles)
            subtitle.RemoveUnneededCharactersFromSubtitles();

        await db.BombaSubtitles.AddRangeAsync(bombaSubtitles);
        await db.SaveChangesAsync();
    }
}