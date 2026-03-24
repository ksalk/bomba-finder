namespace Bomba.DB;

public class QueryCacheEntry
{
    public string NormalizedQuery { get; set; } = string.Empty;
    public Guid ScriptChunkId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastAccessedAt { get; set; }
    public int AccessCount { get; set; }
}
