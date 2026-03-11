using System.Diagnostics;
using Bomba.DB;
using Whisper.net;

public static class AudioTranscriber
{
    private static readonly Lazy<WhisperProcessor> whisperProcessor = new(() => GetWhisperProcessor());

    public static async Task<ExtractedScript> Transcribe(Stream audioStream)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var segments = new List<ScriptSegment>();

        await foreach (var segment in whisperProcessor.Value.ProcessAsync(audioStream))
        {
            segments.Add(new ScriptSegment(segment.Text, segment.Start, segment.End));
        }

        Console.WriteLine();
        var transcriptionDuration = Stopwatch.GetElapsedTime(startTimestamp);
        Console.WriteLine($"[STT] Transcription completed in {transcriptionDuration.TotalSeconds:F2} seconds.");

        return new ExtractedScript
        {
            Text = string.Concat(segments.Select(s => s.Text)).Trim(),
            ExtractionType = ExtractionType.STT,
            Segments = segments
        };
    }

    private static WhisperProcessor GetWhisperProcessor()
    {
        //var modelPath = "ggml-large-v3.bin";
        var modelPath = "ggml-medium.bin";
        var factory = WhisperFactory.FromPath(modelPath);
        return factory.CreateBuilder()
            .WithProgressHandler((progress) => { Console.Write($"\r[STT] Transcription progress: {progress}%  "); })
            .WithLanguage("pl")
            .Build();
    }
}
