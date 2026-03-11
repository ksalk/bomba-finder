using System.Diagnostics;

public static class YtDlp
{
    public static async Task<string> RunAsync(string arguments)
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

        using var process = Process.Start(psi)!;

        string stdout = await process.StandardOutput.ReadToEndAsync();
        string stderr = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new Exception(stderr);

        return stdout;
    }
}
