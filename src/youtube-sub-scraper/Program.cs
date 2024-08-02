﻿// See https://aka.ms/new-console-template for more information
using YoutubeExplode.Playlists;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.ClosedCaptions;
using System.Text;
using YoutubeSubScraper;
using System.Text.RegularExpressions;

// url to bomba playlist
var playlistUrl = "https://www.youtube.com/playlist?list=PLHtUOYOPwzJGGZkjR-FspIL17YtSBGaCR"; // Replace with your YouTube playlist URL

var videoUrls = await GetVideoUrlsFromPlaylist(playlistUrl);

List<BombaSubtitles> bombaSubtitles = new List<BombaSubtitles>();
foreach (var videoUrl in videoUrls)
{
    var details = await GetSubtitlesForUrl(videoUrl);
    if (details != null)
        bombaSubtitles.AddRange(details);
}

await SaveBombaSubtitlesToDb(bombaSubtitles);

await RemoveUnneededCharactersFromBombaSubtitles();

string ClosedCaptionTrackToTxt(ClosedCaptionTrack track)
{
    var sb = new List<string>();
    foreach (var caption in track.Captions)
    {
        sb.Add(caption.Text.TrimStart(new char[] { ' ', '-' }));
    }

    return string.Join(" ", sb).Replace(Environment.NewLine, string.Empty);
}

async Task<List<BombaSubtitles>> GetSubtitlesForUrl(string videoUrl)
{
    try
    {
        var youtube = new YoutubeClient();
        // Get the video ID from the URL
        var videoId = VideoId.Parse(videoUrl);

        // Get video information
        var video = await youtube.Videos.GetAsync(videoId);

        // Get available closed caption tracks
        var trackManifest = await youtube.Videos.ClosedCaptions.GetManifestAsync(videoId);

        // Select the first English track
        var trackInfo = trackManifest.Tracks.FirstOrDefault(t => t.Language.Code == "pl" && !t.IsAutoGenerated);
        var tracksLanguages = string.Join(", ", trackManifest.Tracks.Select(x => x.Language)) ?? "";
        Console.WriteLine($"{video.Title}\nFound subtitles: {tracksLanguages}\n\n");

        if (trackInfo != null)
        {
            if (video.Title.Contains("LASER"))
                Console.WriteLine("");
            // Download the closed caption track
            var track = await youtube.Videos.ClosedCaptions.GetAsync(trackInfo);

            // Convert to SRT format
            var txt = ClosedCaptionTrackToTxt(track);

            return track
                .Captions
                .Select(c => new BombaSubtitles(video.Title, videoUrl, c.Text, c.Offset))
                .ToList();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred: {ex.Message}");
    }
    return null;
}

async Task<List<string>> GetVideoUrlsFromPlaylist(string playlistUrl)
{
    var youtube = new YoutubeClient();

    try
    {
        // Get the playlist ID from the URL
        var playlistId = PlaylistId.Parse(playlistUrl);

        // Get the playlist information
        var playlist = await youtube.Playlists.GetAsync(playlistId);

        // Get all video IDs from the playlist
        var videoIds = youtube.Playlists.GetVideosAsync(playlistId);

        List<string> videoUrls = new List<string>();
        // Print each video URL
        await foreach (var video in videoIds)
        {
            videoUrls.Add($"https://www.youtube.com/watch?v={video.Id}");
            Console.WriteLine($"https://www.youtube.com/watch?v={video.Id}");
        }

        Console.WriteLine($"Total videos in the playlist: {videoUrls.Count}");

        return videoUrls;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred: {ex.Message}");
    }
    return new List<string>();
}

async Task SaveBombaSubtitlesToDb(List<BombaSubtitles> bombaSubtitles)
{
    using (var db = new BombaDbContext())
    {
        db.Database.EnsureCreated();

        await db.BombaSubtitles.AddRangeAsync(bombaSubtitles);
        await db.SaveChangesAsync();
    }
}

async Task RemoveUnneededCharactersFromBombaSubtitles()
{
    using (var db = new BombaDbContext())
    {
        db.Database.EnsureCreated();

        var subtitles = db.BombaSubtitles.ToList();
        foreach (var item in subtitles)
        {
            item.RemoveUnneededCharactersFromSubtitles();
        }
        await db.SaveChangesAsync();
    }
}

