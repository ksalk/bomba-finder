using YoutubeSubScraper;
using YoutubeSubScraper.Persistence;

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

List<BombaSubtitles> bombaSubtitles = new List<BombaSubtitles>();
foreach (var videoUrl in videoUrls)
{
    // Try to download YT captions first
    var subtitles = await Youtube.GetCaptionsForVideo(videoUrl);
    if (subtitles.Any())
    {
        bombaSubtitles.AddRange(subtitles);
        continue;
    }

    // If no YT captions are available, attempt to use Azure Speech-to-Text
    subtitles = await AzureSpeechStudioSubtitlesProvider.Provide(videoUrl);
    if (subtitles.Any())
    {
        bombaSubtitles.AddRange(subtitles);
        continue;
    }
}

//await Persistence.SaveBombaSubtitlesToDb(bombaSubtitles);