using YoutubeDLSharp;
using YoutubeDLSharp.Options;

namespace YoutubeSubScraper;

public static class YoutubeDownloader
{
    public static async Task<string> SaveAudioToWavFile(string videoUrl)
    { 
        var ytDlpFilepath = Path.Combine(Directory.GetCurrentDirectory(), "yt-dlp.exe");
        var ffmpegFilepath = Path.Combine(Directory.GetCurrentDirectory(), "ffmpeg.exe");
        
        if(!File.Exists(ytDlpFilepath))
            await YoutubeDLSharp.Utils.DownloadYtDlp();
        if(!File.Exists(ffmpegFilepath))
            await YoutubeDLSharp.Utils.DownloadFFmpeg();
        
        var ytdl = new YoutubeDL();
        ytdl.YoutubeDLPath = ytDlpFilepath;
        ytdl.FFmpegPath = ffmpegFilepath;
        ytdl.OutputFolder = Path.Combine(Directory.GetCurrentDirectory(), "OutputAudioFiles");

        if (!Directory.Exists(ytdl.OutputFolder))
            Directory.CreateDirectory(ytdl.OutputFolder);

        var res = await ytdl.RunAudioDownload(
            videoUrl,
            AudioConversionFormat.Wav
        );

        string path = res.Data;
        return path;
    }
}