using Bomba.DB;

public static class SubtitlesTranscriber
{
    public static async Task<ExtractedScript> Transcribe(string subtitlesFilePath)
    {
        var segments = new List<ScriptSegment>();

        var lines = await File.ReadAllLinesAsync(subtitlesFilePath);
        foreach (var line in lines)
        {
            // TODO: fix this
            if (line.Contains(" --> ") &&
                TimeSpan.TryParse(line.Split(" --> ")[0], out var start) &&
                TimeSpan.TryParse(line.Split(" --> ")[1], out var end))
            {
                var text = string.Join(' ', lines.SkipWhile(l => l != line).Skip(1).TakeWhile(l => !string.IsNullOrWhiteSpace(l)));
                segments.Add(new ScriptSegment(text, start, end));
            }
        }

        return new ExtractedScript
        {
            Text = string.Concat(segments.Select(s => s.Text)).Trim(),
            ExtractionType = ExtractionType.Subtitles,
            Segments = segments
        };
    }
}