using Microsoft.Extensions.Configuration;
using YoutubeExplode.Videos;
using YoutubeSubScraper;
using YoutubeSubScraper.Persistence;

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

// TODO: make this a collection of videoIds or videoContexts, not urls
videoUrls = videoUrls.Distinct().ToList();
var videoIdsInDb = await Persistence.GetVideoIdsFromDb(dbFileName);

var secondsProcessed = 0.0;
var maxSecondsProcessed = TimeSpan.FromMinutes(10).TotalSeconds;

var bombaSubtitles = new List<BombaSubtitles>();
foreach (var videoUrl in videoUrls)
{
    Console.WriteLine();
    
    // TODO: add logging
    var video = await Youtube.GetVideo(videoUrl);
    if (video == null)
    {
        Console.WriteLine($"Could not find video: {videoUrl}");
        continue;
    }

    var videoId = VideoId.Parse(videoUrl).ToString();
    if (videoIdsInDb.Contains(videoId))
    {
        Console.WriteLine($"{videoId}: Subtitles already in db for video: \"{video.Title}\". Skipping.");
        continue;
    }
    
    Console.WriteLine($"{videoId}: Attempting to get subtitles for video: \"{video.Title}\"");
        
    // Try to download YT captions first
    var subtitles = await Youtube.GetCaptionsForVideo(videoUrl);
    if (subtitles.Any())
    {
        Console.WriteLine($"{videoId}: Found YouTube subtitles");
        bombaSubtitles.AddRange(subtitles);
        continue;
    }
    
    Console.WriteLine($"{videoId}: No YouTube subtitles found");

    if (USE_AZURE_SPEECH_TO_TEXT)
    {
        // If no YT captions are available, attempt to use Azure Speech-to-Text
        Console.WriteLine($"{videoId}: Attempting to use Azure AI to recognize speech for video: \"{video.Title}\"");
        var audioWavFilePath = await YoutubeDownloader.SaveAudioToWavFile(videoUrl);
        
        subtitles = await AzureSpeechToText.ProcessSpeechFromWavFile(audioWavFilePath);
        
        secondsProcessed += video.Duration?.TotalSeconds ?? 0;
        Console.WriteLine($"{videoId}: Processed {secondsProcessed} seconds. Its {secondsProcessed / 60} minutes. Should not exceed {TimeSpan.FromSeconds(maxSecondsProcessed).TotalMinutes} minutes.");
        
        if (secondsProcessed > maxSecondsProcessed)
        {
            Console.WriteLine($"{videoId}: Stopping AI Speech recognition - AI processed limit reached.");
            USE_AZURE_SPEECH_TO_TEXT = false;
        }
        
        if (subtitles.Any())
        {
            Console.WriteLine($"{videoId}: Subtitles found with Azure AI");
            subtitles.ForEach(s =>
            {
                s.VideoUrl = videoUrl;
                s.VideoId = videoId;
                s.Title = video.Title;
            });
            bombaSubtitles.AddRange(subtitles);
            continue;
        }
        
        Console.WriteLine($"{videoId}: No subtitles found with Azure AI");
    }
}

if (bombaSubtitles.Any())
{
    Console.WriteLine($"Bomba Subtitles Found: {bombaSubtitles.Count}");
    //var runId = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
    await Persistence.SaveBombaSubtitlesToDb(bombaSubtitles, dbFileName);
}
else
{
    Console.WriteLine("No Bomba Subtitles Found");
}