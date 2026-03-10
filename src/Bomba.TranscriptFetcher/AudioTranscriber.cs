using Whisper.net;

public static class AudioTranscriber
{
    private static readonly Lazy<WhisperProcessor> whisperProcessor = new(() => GetWhisperProcessor());

    public static async Task<string> Transcribe(Stream audioStream)
    {
        var segments = new List<string>();

        await foreach (var segment in whisperProcessor.Value.ProcessAsync(audioStream))
        {
            segments.Add(segment.Text);
        }

        Console.WriteLine();
        return string.Concat(segments).Trim();
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