using Bomba.DB;

public class ScriptExtractor
{
    public static async Task ExtractScriptsForPlaylist(BombaDbContext bombaDb, string youtubePlaylistUrl, bool extractOnlyMissing, bool showSkipInfo)
    {
        var videosMetadata = await YoutubeMetadataDownloader.GetPlaylistVideosAsync(youtubePlaylistUrl);

        foreach (var videoMetadata in videosMetadata)
        {
            var existingEntry = bombaDb.VideoScripts.FirstOrDefault(vs => vs.VideoId == videoMetadata.Id);
            if (existingEntry != null && extractOnlyMissing)
            {
                if (showSkipInfo)
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

        var allVideoIdsInDb = bombaDb.VideoScripts.Select(vs => vs.VideoId).ToHashSet();
        var missingVideos = videosMetadata.Where(vm => !allVideoIdsInDb.Contains(vm.Id)).ToList();

        if (missingVideos.Count > 0)
        {
            Console.WriteLine($"[MAIN] {missingVideos.Count} videos from the playlist are missing in the database after processing:");
        }

        foreach (var missingVideo in missingVideos)
        {
            Console.WriteLine($"[MAIN] Missing video in DB: {missingVideo}");
        }
    }

    private static async Task<ExtractedScript?> TryGettingVideoScript(YoutubeVideoMetadata videoMetadata)
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
}