using Bomba.DB;

public static class ScriptChunker
{
    private const int ChunkSizeThreshold = 50;

    public static List<ScriptChunk> GetScriptChunks(VideoScript videoScript)
    {
        var chunks = new List<ScriptChunk>();

        for(var i = 0 ; i < videoScript.Segments.Count; i++)
        {
            var chunkSegments = new List<ScriptSegment>() { videoScript.Segments[i] };

            var j = i;
            while (j + 1 < videoScript.Segments.Count && chunkSegments.Sum(s => s.Text.Length) < ChunkSizeThreshold)
            {
                chunkSegments.Add(videoScript.Segments[++j]);
            }

            var chunkText = string.Join(" ", chunkSegments.Select(s => s.Text)).Trim();

            chunks.Add(new ScriptChunk
            {
                VideoScriptId = videoScript.Id,
                Text = chunkText,
                Start = chunkSegments.First().Start,
                End = chunkSegments.Last().End
            });

            if(j == videoScript.Segments.Count - 1)
                break;            
        }
    
        return chunks;
    }
}