
var videoUrl = "https://www.youtube.com/watch?v=d4mH_k437jM";
//var videoUrl = "https://www.youtube.com/watch?v=HJHxU_QPD5c"; // yeti

// 0. Try downloading subtitles first, if available, to save time and resources
var subtitleOutputPath = "output/subtitles.srt";
var subtitlesDownloaded = await YoutubeDownloader.DownloadSubtitles(videoUrl, subtitleOutputPath);
var extractedScript = subtitlesDownloaded
    ? await SubtitlesTranscriber.Transcribe(subtitleOutputPath)
    : null;

if (extractedScript is null)
{
    Console.WriteLine("[MAIN] No subtitles available, falling back to audio transcription...");
    // 1. Fetch video -> audio from YT
    var outputPath = "output/audio.wav";
    var audioDownloaded = await YoutubeDownloader.DownloadAudio(videoUrl, outputPath);

    if (!audioDownloaded)
    {
        Console.WriteLine("[MAIN] Failed to download audio, cannot proceed with transcription.");
        return;
    }

    // 2. Transcribe audio to text using Whisper.NET
    var audioStream = File.OpenRead(outputPath);
    extractedScript = await AudioTranscriber.Transcribe(audioStream);
    //Console.WriteLine(extractedScript.Text);
}

// 2.5 Consider transcript chunking for better context handling and retrieval

// 3. Store trascript to VectorDB