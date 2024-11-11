using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using YoutubeExplode.Videos;
using YoutubeSubScraper;
using YoutubeSubScraper.Persistence;

bool useAzureSpeechToText = true;
const string dbFileName = "bomba-subtitles.db";
var maxSecondsProcessed = TimeSpan.FromMinutes(10).TotalSeconds;

Initialize();

Log.Logger.Information("Starting up Youtube Sub Scraper");

var videoIds = await GetVideoIdsForProcessing();
var videoIdsInDb = await Persistence.GetVideoIdsFromDb(dbFileName);

var secondsProcessed = 0.0;
var bombaSubtitles = new List<BombaSubtitles>();
foreach (var videoId in videoIds)
{
    Log.Logger.Information($"{videoId}: Attempting to retrieve video details from Youtube.");
    var video = await Youtube.GetVideo(videoId);
    if (video == null)
    {
        Log.Logger.Warning($"Could not find video with id: {videoId}");
        continue;
    }

    if (videoIdsInDb.Contains(videoId))
    {
        Log.Logger.Information($"{videoId}: Subtitles already in db for video: \"{video.Title}\". Skipping.");
        continue;
    }
    
    Log.Logger.Information($"{videoId}: Attempting to get subtitles for video: \"{video.Title}\"");
        
    // Try to download YT captions first
    var subtitles = await Youtube.GetCaptionsForVideo(video.Url);
    if (subtitles.Any())
    {
        Log.Logger.Information($"{videoId}: Found YouTube subtitles");
        bombaSubtitles.AddRange(subtitles);
        continue;
    }
    
    Log.Logger.Information($"{videoId}: No YouTube subtitles found");

    if (useAzureSpeechToText)
    {
        // If no YT captions are available, attempt to use Azure Speech-to-Text
        Log.Logger.Information($"{videoId}: Attempting to use Azure AI to recognize speech for video: \"{video.Title}\"");
        var audioWavFilePath = await Youtube.SaveAudioToWavFile(video.Url);
        
        subtitles = await AzureSpeechToText.ProcessSpeechFromWavFile(audioWavFilePath);
        
        secondsProcessed += video.Duration?.TotalSeconds ?? 0;
        Log.Logger.Information($"{videoId}: Processed {secondsProcessed} seconds. Its {secondsProcessed / 60} minutes. Should not exceed {TimeSpan.FromSeconds(maxSecondsProcessed).TotalMinutes} minutes.");
        
        if (secondsProcessed > maxSecondsProcessed)
        {
            Log.Logger.Warning($"{videoId}: Stopping AI Speech recognition - AI processed limit reached.");
            useAzureSpeechToText = false;
        }
        
        if (subtitles.Any())
        {
            Log.Logger.Information($"{videoId}: Subtitles found with Azure AI");
            subtitles.ForEach(s => s.UpdateVideoDetails(video));
            bombaSubtitles.AddRange(subtitles);
            continue;
        }
        
        Log.Logger.Information($"{videoId}: No subtitles found with Azure AI");
    }
}

Log.Logger.Information($"Subtitles found: {bombaSubtitles.Count}. Saving to {dbFileName}");
await Persistence.SaveBombaSubtitlesToDb(bombaSubtitles, dbFileName);

Log.Logger.Information("Processing finished");

async Task<List<VideoId>> GetVideoIdsForProcessing()
{
    var playlistUrls = new List<string>()
    {
        "https://www.youtube.com/playlist?list=PLHtUOYOPwzJGGZkjR-FspIL17YtSBGaCR"
    };
    var videoUrls = new List<string>() { };

    foreach (var playlistUrl in playlistUrls)
        videoUrls.AddRange(await Youtube.GetVideoUrlsFromPlaylistUrl(playlistUrl));

    return videoUrls
        .Distinct()
        .Select(VideoId.Parse)
        .ToList();
}

void Initialize()
{
    var runId = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.File(Path.Combine(Directory.GetCurrentDirectory(), "Logs", $"log-{runId}.txt"),
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information)
        .CreateLogger();

    var environment = Environment.GetEnvironmentVariable("NETCORE_ENVIRONMENT");
    var builder = new ConfigurationBuilder()
        .AddJsonFile("appSettings.json")
        .AddJsonFile($"appSettings.{environment}.json", optional: true);

    var azureConfigSection = builder
        .Build()
        .GetSection("Azure");
    AzureSpeechToText.SetSubscriptionKey(azureConfigSection["SubscriptionKey"] ?? string.Empty);
}