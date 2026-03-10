
using Bomba.DB;

var EXTRACT_SCRIPTS = true;
var EXTRACT_ONLY_MISSING = false; // TODO: handle extracting only missing scripts

// TODO: export / import scripts as JSON to avoid re-processing during development and testing

var bombaDb = new BombaDbContext();
await bombaDb.Database.EnsureCreatedAsync();

if (EXTRACT_SCRIPTS)
{
    Console.WriteLine("[MAIN] Starting script extraction process...");
    var videoPlaylist = "https://www.youtube.com/playlist?list=PLHtUOYOPwzJGGZkjR-FspIL17YtSBGaCR";
    var videosMetadata = await YoutubeMetadataDownloader.GetPlaylistVideosAsync(videoPlaylist);

    foreach (var videoMetadata in videosMetadata)
    {
        Console.WriteLine($"[MAIN] Processing video: {videoMetadata}");

        var script = await TryGettingVideoScript(videoMetadata);
        if (script is null)
        {
            Console.WriteLine($"[MAIN] Failed to extract script for video: {videoMetadata}");
            continue;
        }

        // Store the extracted script in the database
        var existingEntry = bombaDb.VideoScripts.FirstOrDefault(vs => vs.VideoId == videoMetadata.Id);
        if (existingEntry != null)
        {
            existingEntry.Transcript = script.Text;
            existingEntry.Segments = script.Segments.ToList();
        }
        else
        {
            var videoEntry = new VideoScript
            {
                VideoId = videoMetadata.Id,
                Title = videoMetadata.Title,
                VideoUrl = videoMetadata.Url,
                Transcript = script.Text,
                Segments = script.Segments.ToList()
            };
            bombaDb.VideoScripts.Add(videoEntry);
        }

        Console.WriteLine($"[MAIN] Successfully processed video: {videoMetadata}");
        await bombaDb.SaveChangesAsync();
    }

    Console.WriteLine("[MAIN] Script extraction process completed.");
}

//var allScripts = bombaDb.VideoScripts.ToList();
//Console.WriteLine($"Total scripts extracted and stored in DB: {allScripts.Count}");


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