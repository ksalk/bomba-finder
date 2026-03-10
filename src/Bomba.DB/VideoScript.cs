namespace Bomba.DB;

public class VideoScript
{
    public Guid Id { get; set; }
    public string VideoUrl { get; set; }
    public string VideoId { get; set; }
    public string Title { get; set; }
    public string Transcript { get; set; }
    // As JSONB in DB
    public List<ScriptSegment> Segments { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual List<ScriptChunk> Chunks { get; set; }
}
