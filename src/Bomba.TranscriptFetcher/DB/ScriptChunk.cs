using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

public class ScriptChunk
{
    public Guid Id { get; set; }
    public Guid VideoScriptId { get; set; }
    public string Text { get; set; }
    public TimeSpan Start { get; set; }
    public TimeSpan End { get; set; }
    [Column(TypeName = "vector(3)")]
    public Vector? Embedding { get; set; }
}