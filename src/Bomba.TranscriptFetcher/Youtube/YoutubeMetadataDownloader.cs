public record YoutubeVideoMetadata(string Id, string Title, string Url)
{
    public override string ToString() => $"{Title} ({Id})";
}

public static class YoutubeMetadataDownloader
{
    public static async Task<IReadOnlyList<YoutubeVideoMetadata>> GetPlaylistVideosAsync(string playlistUrl)
    {
        // --flat-playlist skips downloading any media — only fetches playlist metadata
        // --print outputs a formatted line per entry; tab-separated id + title is safe and easy to parse
        var arguments = $"--flat-playlist --print \"%(id)s\t%(title)s\" \"{playlistUrl}\"";

        Console.WriteLine($"[YT] Fetching playlist metadata: {playlistUrl}");

        string stdout;
        try
        {
            stdout = await YtDlp.RunAsync(arguments);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[YT] Error fetching playlist metadata: {ex.Message}");
            return [];
        }

        var results = new List<YoutubeVideoMetadata>();

        foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t', 2);
            if (parts.Length != 2)
                continue;

            var id = parts[0].Trim();
            var title = parts[1].Trim();
            var url = $"https://www.youtube.com/watch?v={id}";

            results.Add(new YoutubeVideoMetadata(id, title, url));
        }

        Console.WriteLine($"[YT] Found {results.Count} videos in playlist.");
        return results;
    }
}
