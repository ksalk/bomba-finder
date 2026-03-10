using Whisper.net;

public static class AudioTranscriber
{
    private static readonly Lazy<WhisperProcessor> whisperProcessor = new(() => GetWhisperProcessor());

    public static async Task<TranscriptionResult> Transcribe(Stream audioStream)
    {
        var segments = new List<SegmentData>();

        await foreach (var segment in whisperProcessor.Value.ProcessAsync(audioStream))
        {
            segments.Add(segment);
        }

        Console.WriteLine();
        return new TranscriptionResult
        {
            Text = string.Concat(segments.Select(s => s.Text)).Trim(),
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

public class TranscriptionResult
{
    public required string Text { get; set; }
    public required IReadOnlyList<SegmentData> Segments { get; set; }
}