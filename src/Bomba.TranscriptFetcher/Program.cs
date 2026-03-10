
//var videoUrl = "https://www.youtube.com/watch?v=d4mH_k437jM";
var videoUrl = "https://www.youtube.com/watch?v=HJHxU_QPD5c"; // yeti

// 0. Try downloading subtitles first, if available, to save time and resources
var subtitleOutputPath = "output/subtitles.srt";
var subtitlesDownloaded = await YoutubeDownloader.DownloadSubtitles(videoUrl, subtitleOutputPath);

if(subtitlesDownloaded)
{
    Console.WriteLine($"[Main] Subtitles downloaded successfully to {subtitleOutputPath}. Consider parsing and using them instead of audio transcription.");
}
else
{
    Console.WriteLine($"[Main] Subtitles not available for video {videoUrl}. Proceeding with audio transcription.");
}

// 1. Fetch video -> audio from YT
var outputPath = "output/audio.wav";
var audioDownloaded = await YoutubeDownloader.DownloadAudio(videoUrl, outputPath);

// 2. Transcribe audio to text using Whisper.NET
//var audioStream = File.OpenRead(outputPath);
// var transcript = await AudioTranscriber.Transcribe(audioStream);
// Console.WriteLine(transcript.Text);

// 2.5 Consider transcript chunking for better context handling and retrieval

// 3. Store trascript to VectorDB