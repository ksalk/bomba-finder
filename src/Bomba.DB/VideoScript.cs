namespace Bomba.DB;

public class VideoScript
{
    public Guid Id { get; set; }
    public string VideoUrl { get; set; }
    public string VideoId { get; set; }
    public string Title { get; set; }
    public string Transcript { get; set; }
    public ExtractionType ExtractionType { get; set; }

    public List<ScriptSegment> Segments { get; set; }

    public virtual List<ScriptChunk> Chunks { get; set; }

}

public enum ExtractionType
{
    Subtitles,
    STT
}