
using System.Text.Json;
using Bomba.DB;

var EXTRACT_SCRIPTS = false;
var EXTRACT_ONLY_MISSING = true;
var SHOW_SKIP_INFO = false;

// TODO: export / import scripts as JSON to avoid re-processing during development and testing

var bombaDb = new BombaDbContext();
await bombaDb.Database.EnsureCreatedAsync();

var videoPlaylist = "https://www.youtube.com/playlist?list=PLHtUOYOPwzJGGZkjR-FspIL17YtSBGaCR";
var videosMetadata = await YoutubeMetadataDownloader.GetPlaylistVideosAsync(videoPlaylist);

if (EXTRACT_SCRIPTS)
{
    Console.WriteLine("[MAIN] Starting script extraction process...");

    foreach (var videoMetadata in videosMetadata)
    {
        var existingEntry = bombaDb.VideoScripts.FirstOrDefault(vs => vs.VideoId == videoMetadata.Id);
        if (existingEntry != null && EXTRACT_ONLY_MISSING)
        {
            if(SHOW_SKIP_INFO)
                Console.WriteLine($"[MAIN] Script already exists for video: {videoMetadata}, skipping...");
            continue;
        }

        Console.WriteLine($"[MAIN] Processing video: {videoMetadata}");

        var script = await TryGettingVideoScript(videoMetadata);
        if (script is null)
        {
            Console.WriteLine($"[MAIN] Failed to extract script for video: {videoMetadata}");
            continue;
        }

        // Store the extracted script in the database        
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

var allVideoIdsInDb = bombaDb.VideoScripts.Select(vs => vs.VideoId).ToHashSet();
var missingVideos = videosMetadata.Where(vm => !allVideoIdsInDb.Contains(vm.Id)).ToList();

if(missingVideos.Count > 0)
{
    Console.WriteLine($"[MAIN] {missingVideos.Count} videos from the playlist are missing in the database after processing:");
}

foreach (var missingVideo in missingVideos)
{
    Console.WriteLine($"[MAIN] Missing video in DB: {missingVideo}");
}

// 2.5 Consider transcript chunking for better context handling and retrieval
var firstVideoWithScript = bombaDb.VideoScripts.FirstOrDefault();
if (firstVideoWithScript != null)
{
    var chunks = ScriptChunker.GetScriptChunks(firstVideoWithScript);
    Console.WriteLine($"[MAIN] Script chunking done for video: {firstVideoWithScript.VideoId}");

    firstVideoWithScript.Chunks.Clear();
    firstVideoWithScript.Chunks.AddRange(chunks);
    await bombaDb.SaveChangesAsync();
}

// 3. Store trascript to VectorDB


async Task<ExtractedScript?> TryGettingVideoScript(YoutubeVideoMetadata videoMetadata)
{
    // Try downloading subtitles first, if available
    var downloadedSubtitlePath = await YoutubeDownloader.DownloadSubtitles(videoMetadata.Url);
    if (downloadedSubtitlePath != null)
    {
        var extractedSubtitlesScript = await SubtitlesTranscriber.Transcribe(downloadedSubtitlePath);
        File.Delete(downloadedSubtitlePath);
        return extractedSubtitlesScript;
    }

    Console.WriteLine("[MAIN] No subtitles available, falling back to audio transcription...");

    // Fetch audio from YT
    var audioOutputPath = $"output/audio-{videoMetadata.Id}.wav";
    var audioDownloaded = await YoutubeDownloader.DownloadAudio(videoMetadata.Url, audioOutputPath);
    if (!audioDownloaded)
    {
        Console.WriteLine("[MAIN] Failed to download audio, cannot proceed with transcription.");
        return null;
    }

    // Transcribe audio to text using Whisper.NET
    var extractedScript = await AudioTranscriber.Transcribe(audioOutputPath);
    File.Delete(audioOutputPath);
    return extractedScript;
}

async Task ExportBombaDbToJson(string filePath)
{
    var allScripts = bombaDb.VideoScripts.ToList();
    var json = JsonSerializer.Serialize(allScripts, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(filePath, json);
}