
using Bomba.DB;

var videoPlaylist = "https://www.youtube.com/playlist?list=PLHtUOYOPwzJGGZkjR-FspIL17YtSBGaCR";
var videosMetadata = await YoutubeMetadataDownloader.GetPlaylistVideosAsync(videoPlaylist);
var bombaDb = new BombaDbContext();

foreach (var videoMetadata in videosMetadata)
{
    Console.WriteLine($"Processing video: {videoMetadata}");

    var script = await TryGettingVideoScript(videoMetadata);
    if(script is null)
    {
        Console.WriteLine($"Failed to extract script for video: {videoMetadata}");
        continue;
    }

    // Store the extracted script in the database
    var videoEntry = new VideoScript
    {
        VideoId = videoMetadata.Id,
        Title = videoMetadata.Title,
        VideoUrl = videoMetadata.Url,
        Transcript = script.Text,
        Segments = script.Segments.ToList()
    };
    bombaDb.VideoScripts.Add(videoEntry);
    await bombaDb.SaveChangesAsync();
}

// 2.5 Consider transcript chunking for better context handling and retrieval

// 3. Store trascript to VectorDB


async Task<ExtractedScript?> TryGettingVideoScript(YoutubeVideoMetadata videoMetadata)
{
    // Try downloading subtitles first, if available, to save time and resources
    var subtitleOutputPath = $"output/subtitles-{videoMetadata.Id}.srt";
    var subtitlesDownloaded = await YoutubeDownloader.DownloadSubtitles(videoMetadata.Url, subtitleOutputPath);
    if (subtitlesDownloaded)
    {
        var extractedScript = await SubtitlesTranscriber.Transcribe(subtitleOutputPath);
        return extractedScript;
    }

    Console.WriteLine("[MAIN] No subtitles available, falling back to audio transcription...");

    // Fetch audio from YT
    var outputPath = $"output/audio-{videoMetadata.Id}.wav";
    var audioDownloaded = await YoutubeDownloader.DownloadAudio(videoMetadata.Url, outputPath);

    if (!audioDownloaded)
    {
        Console.WriteLine("[MAIN] Failed to download audio, cannot proceed with transcription.");
        return null;
    }

    // Transcribe audio to text using Whisper.NET
    var audioStream = File.OpenRead(outputPath);
    return await AudioTranscriber.Transcribe(audioStream);
}