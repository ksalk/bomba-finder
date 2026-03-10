
// 1. Fetch video -> audio from YT
var videoUrl = "https://www.youtube.com/watch?v=d4mH_k437jM";
var outputPath = "output/audio.wav";
await YoutubeDownloader.DownloadAudio(videoUrl, outputPath);

// 2. Transcribe audio to text using Whisper.NET
var audioStream = File.OpenRead(outputPath);
var transcript = await AudioTranscriber.Transcribe(audioStream);
Console.WriteLine(transcript.Text);

// 2.5 Consider transcript chunking for better context handling and retrieval

// 3. Store trascript to VectorDB