using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using YoutubeExplode.Videos;
using YoutubeSubScraper;
using YoutubeSubScraper.Persistence;

var runId = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File(Path.Combine(Directory.GetCurrentDirectory(), "Logs", $"log-{runId}.txt"),
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information)

    .CreateLogger();

bool USE_AZURE_SPEECH_TO_TEXT = true;
const string dbFileName = "bomba-subtitles.db"; 

var environment = Environment.GetEnvironmentVariable("NETCORE_ENVIRONMENT");
var builder = new ConfigurationBuilder()
    .AddJsonFile("appSettings.json")
    .AddJsonFile($"appSettings.{environment}.json", optional: true);

var azureConfigSection = builder
    .Build()
    .GetSection("Azure");
AzureSpeechToText.SetSubscriptionKey(azureConfigSection["SubscriptionKey"] ?? string.Empty);

var playlistUrls = new List<string>()
{
    "https://www.youtube.com/playlist?list=PLHtUOYOPwzJGGZkjR-FspIL17YtSBGaCR"
}; 
var videoUrls = new List<string>()
{
    //"https://www.youtube.com/watch?v=Oykvszo9csA",
    //"https://www.youtube.com/watch?v=2JWOdqS7fI4"
};

foreach (var playlistUrl in playlistUrls)
    videoUrls.AddRange(await Youtube.GetVideoUrlsFromPlaylistUrl(playlistUrl));

var videoIds = videoUrls
    .Distinct()
    .Select(VideoId.Parse)
    .ToList();

var videoIdsInDb = await Persistence.GetVideoIdsFromDb(dbFileName);

var secondsProcessed = 0.0;
var maxSecondsProcessed = TimeSpan.FromMinutes(10).TotalSeconds;

var bombaSubtitles = new List<BombaSubtitles>();
foreach (var videoId in videoIds)
{
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

    if (USE_AZURE_SPEECH_TO_TEXT)
    {
        // If no YT captions are available, attempt to use Azure Speech-to-Text
        Log.Logger.Information($"{videoId}: Attempting to use Azure AI to recognize speech for video: \"{video.Title}\"");
        var audioWavFilePath = await YoutubeDownloader.SaveAudioToWavFile(video.Url);
        
        subtitles = await AzureSpeechToText.ProcessSpeechFromWavFile(audioWavFilePath);
        
        secondsProcessed += video.Duration?.TotalSeconds ?? 0;
        Log.Logger.Information($"{videoId}: Processed {secondsProcessed} seconds. Its {secondsProcessed / 60} minutes. Should not exceed {TimeSpan.FromSeconds(maxSecondsProcessed).TotalMinutes} minutes.");
        
        if (secondsProcessed > maxSecondsProcessed)
        {
            Log.Logger.Warning($"{videoId}: Stopping AI Speech recognition - AI processed limit reached.");
            USE_AZURE_SPEECH_TO_TEXT = false;
        }
        
        if (subtitles.Any())
        {
            Log.Logger.Information($"{videoId}: Subtitles found with Azure AI");
            subtitles.ForEach(s =>
            {
                s.VideoUrl = video.Url;
                s.VideoId = videoId;
                s.Title = video.Title;
            });
            bombaSubtitles.AddRange(subtitles);
            continue;
        }
        
        Log.Logger.Information($"{videoId}: No subtitles found with Azure AI");
    }
}

if (bombaSubtitles.Any())
{
    Log.Logger.Information($"Bomba Subtitles Found: {bombaSubtitles.Count}");
    await Persistence.SaveBombaSubtitlesToDb(bombaSubtitles, dbFileName);
}
else
{
    Log.Logger.Information("No Bomba Subtitles Found");
}