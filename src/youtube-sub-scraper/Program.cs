using Microsoft.Extensions.Configuration;
using YoutubeExplode.Videos;
using YoutubeSubScraper;
using YoutubeSubScraper.Persistence;

const bool USE_AZURE_SPEECH_TO_TEXT = false;

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

// TODO: make this a collection of videoIds, not urls
videoUrls = videoUrls.Distinct().ToList();

var bombaSubtitles = new List<BombaSubtitles>();
foreach (var videoUrl in videoUrls)
{
    // TODO: add logging
    var videoTitle = await Youtube.GetVideoTitle(videoUrl);
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
        var audioWavFilePath = await YoutubeDownloader.SaveAudioToWavFile(videoUrl);
        subtitles = await AzureSpeechToText.ProcessSpeechFromWavFile(audioWavFilePath);
        if (subtitles.Any())
        {
            subtitles.ForEach(s =>
            {
                s.VideoUrl = videoUrl;
                s.VideoId = VideoId.Parse(videoUrl).ToString();
                s.Title = videoTitle;
            });
            bombaSubtitles.AddRange(subtitles);
            continue;
        }
    }
}

if (bombaSubtitles.Any())
{
    Console.WriteLine($"Bomba Subtitles Found: {bombaSubtitles.Count}");
    var runId = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
    await Persistence.SaveBombaSubtitlesToDb(bombaSubtitles, $"bomba-{runId}.db");
}
else
{
    Console.WriteLine("No Bomba Subtitles Found");
}