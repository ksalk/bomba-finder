using System.Diagnostics;

public static class YoutubeDownloader
{
    public static async Task<bool> DownloadAudio(string videoUrl, string outputPath)
    {
        var arguments = $"-f bestaudio -x --audio-format wav --postprocessor-args \"-ar 16000 -ac 1\" -o \"{outputPath}\" {videoUrl}";
        try
        {
            await RunYtDlp(arguments);
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
            var stdout = await RunYtDlp(arguments);
            if(stdout.Contains("There are no subtitles for the requested languages"))
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

    private static async Task<string> RunYtDlp(string arguments)
    {
        Console.WriteLine($"[YT] Running command: yt-dlp {arguments}");
        var psi = new ProcessStartInfo
        {
            FileName = "yt-dlp",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);

        string stdout = await process.StandardOutput.ReadToEndAsync();
        string stderr = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new Exception(stderr);

        return stdout;
    }
}