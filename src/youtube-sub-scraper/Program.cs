﻿using Microsoft.Extensions.Configuration;
using YoutubeSubScraper;
using YoutubeSubScraper.Persistence;

const bool USE_AZURE_SPEECH_TO_TEXT = true;

var environment = Environment.GetEnvironmentVariable("NETCORE_ENVIRONMENT");
var builder = new ConfigurationBuilder()
    .AddJsonFile("appSettings.json")
    .AddJsonFile($"appSettings.{environment}.json", optional: true);

var azureConfigSection = builder
    .Build()
    .GetSection("Azure");
AzureSpeechToText.SetSubcriptionKey(azureConfigSection["SubscriptionKey"] ?? string.Empty);

var playlistUrls = new List<string>()
{
    "https://www.youtube.com/playlist?list=PLHtUOYOPwzJGGZkjR-FspIL17YtSBGaCR"
}; 
var videoUrls = new List<string>()
{
    "https://www.youtube.com/watch?v=6WMl6CSlLos"
};

foreach (var playlistUrl in playlistUrls)
    videoUrls.AddRange(await Youtube.GetVideoUrlsFromPlaylistUrl(playlistUrl));

videoUrls = videoUrls.Distinct().ToList();

var bombaSubtitles = new List<BombaSubtitles>();
foreach (var videoUrl in videoUrls)
{
    // Try to download YT captions first
    var subtitles = await Youtube.GetCaptionsForVideo(videoUrl);
    if (subtitles.Any())
    {
        bombaSubtitles.AddRange(subtitles);
        continue;
    }

    if (USE_AZURE_SPEECH_TO_TEXT)
    {
        // If no YT captions are available, attempt to use Azure Speech-to-Text
        var audioWavFilePath = await Youtube.SaveAudioToWavFile(videoUrl);
        subtitles = await AzureSpeechToText.ProcessSpeechFromWavFile(audioWavFilePath);
        if (subtitles.Any())
        {
            bombaSubtitles.AddRange(subtitles);
            continue;
        }
    }
}

Console.WriteLine($"Bomba Subtitles Found: {bombaSubtitles.Count}");
var runId = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
await Persistence.SaveBombaSubtitlesToDb(bombaSubtitles, $"bomba-{runId}.db");