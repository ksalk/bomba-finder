public class ExtractedScript
{
    public required string Text { get; set; }
    public required IReadOnlyList<ScriptSegment> Segments { get; set; }
}

public record ScriptSegment(string Text, TimeSpan Start, TimeSpan End);