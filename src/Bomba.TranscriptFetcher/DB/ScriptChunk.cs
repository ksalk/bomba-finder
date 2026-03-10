using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

public class ScriptChunk
{
    public Guid Id { get; set; }
    public Guid VideoScriptId { get; set; }
    public virtual VideoScript VideoScript { get; set; }
    public string Text { get; set; }
    public TimeSpan Start { get; set; }
    public TimeSpan End { get; set; }
    public Vector? Embedding { get; set; }
}