using Bomba.DB;

public class ExtractedScript
{
    public required string Text { get; set; }
    public required IReadOnlyList<ScriptSegment> Segments { get; set; }
}
