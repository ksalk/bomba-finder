using System.Diagnostics;

public static class YoutubeDownloader
{
    public static async Task DownloadAudio(string videoUrl, string outputPath)
    {
        var arguments = $"-f bestaudio -x --audio-format wav --postprocessor-args \"-ar 16000 -ac 1\" -o \"{outputPath}\" {videoUrl}";
        try
        {
            await RunYtDlp(arguments);
            Console.WriteLine($"[YT] Audio downloaded for video {videoUrl} to {outputPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[YT] Error downloading audio for video {videoUrl}: {ex.Message}");
            throw;
        }
    }

    private static async Task<string> RunYtDlp(string arguments)
    {
        Console.WriteLine($"[YT] Running yt-dlp with arguments: {arguments}");
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