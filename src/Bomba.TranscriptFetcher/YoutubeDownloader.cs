public static class YoutubeDownloader
{
    public static async Task<bool> DownloadAudio(string videoUrl, string outputPath)
    {
        var arguments = $"-f bestaudio -x --audio-format wav --postprocessor-args \"-ar 16000 -ac 1\" -o \"{outputPath}\" {videoUrl}";
        try
        {
            await YtDlp.RunAsync(arguments);
            Console.WriteLine($"[YT] Audio downloaded for video {videoUrl} to {outputPath}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[YT] Error downloading audio for video {videoUrl}: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> DownloadSubtitles(string videoUrl, string outputPath)
    {
        var arguments = $"-f best --write-subs --sub-lang pl --skip-download -o \"{outputPath}\" {videoUrl}";
        try
        {
            var stdout = await YtDlp.RunAsync(arguments);
            if (stdout.Contains("There are no subtitles for the requested languages"))
            {
                Console.WriteLine($"[YT] No subtitles available for video {videoUrl}");
                return false;
            }

            Console.WriteLine($"[YT] Subtitles downloaded for video {videoUrl} to {outputPath}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[YT] Error downloading subtitles for video {videoUrl}: {ex.Message}");
            return false;
        }
    }
}
