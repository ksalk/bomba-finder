using System.Text.RegularExpressions;
using YoutubeExplode.Videos;

namespace YoutubeSubScraper.Persistence;

public class BombaSubtitles
{
    public Guid Id { get; private set; }
    public string Title { get; private set; }
    public string VideoUrl { get; private set; }
    public string VideoId { get; private set; }
    public string Subtitles { get; private set; }
    public TimeSpan Offset { get; private set; }

    public BombaSubtitles(string title, string videoUrl, string videoId, string subtitles, TimeSpan offset)
    {
        Id = Guid.NewGuid();
        Title = title;
        VideoUrl = videoUrl;
        VideoId = videoId;
        Subtitles = subtitles;
        Offset = offset;

        RemoveUnneededCharactersFromSubtitles();
    }

    public void UpdateVideoDetails(Video video)
    {
        VideoUrl = video.Url;
        VideoId = video.Id;
        Title = video.Title;
    }

    private void RemoveUnneededCharactersFromSubtitles()
    {
        Subtitles = Subtitles.Replace("\r", " ");
        Subtitles = Subtitles.Replace("\n", " ");
        Subtitles = new string(Subtitles.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) && c != '\r' && c != '\n').ToArray()).Trim();
        Subtitles = Regex.Replace(Subtitles, @"\s+", " ");
    }
}