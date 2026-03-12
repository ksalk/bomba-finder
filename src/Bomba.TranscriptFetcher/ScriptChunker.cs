using Bomba.DB;
using Microsoft.EntityFrameworkCore;

public static class ScriptChunker
{
    private static readonly int[] ChunkSizeThresholds = { 30, 50 };

    public static async Task GetScriptChunks(BombaDbContext bombaDb)
    {
        var allVideosInDb = bombaDb.VideoScripts
            .Include(vs => vs.Chunks)
            .Where(vs => vs.Chunks == null || !vs.Chunks.Any())
            .ToList();

        foreach (var videoScript in allVideosInDb)
        {
            var chunks = GetScriptChunks(videoScript);
            Console.WriteLine($"[CHUNKER] Script chunking done for video: {videoScript.VideoId}");

            videoScript.Chunks.Clear();
            videoScript.Chunks.AddRange(chunks);
            await bombaDb.SaveChangesAsync();
        }
    }

    private static List<ScriptChunk> GetScriptChunks(VideoScript videoScript)
    {
        var chunks = new List<ScriptChunk>();

        foreach (var threshold in ChunkSizeThresholds)
        {
            for (var i = 0; i < videoScript.Segments.Count; i++)
            {
                var chunkSegments = new List<ScriptSegment>() { videoScript.Segments[i] };

                var j = i;
                while (j + 1 < videoScript.Segments.Count && chunkSegments.Sum(s => s.Text.Length) < threshold)
                {
                    chunkSegments.Add(videoScript.Segments[++j]);
                }

                var chunkText = string.Join(" ", chunkSegments.Select(s => s.Text)).Trim();

                if (chunks.Any(c => c.Text == chunkText && c.Start == chunkSegments.First().Start && c.End == chunkSegments.Last().End))
                    continue;

                chunks.Add(new ScriptChunk
                {
                    VideoScriptId = videoScript.Id,
                    Text = chunkText,
                    NormalizedText = TextNormalizer.Normalize(chunkText),
                    Start = chunkSegments.First().Start,
                    End = chunkSegments.Last().End
                });

                if (j == videoScript.Segments.Count - 1)
                    break;
            }
        }

        return chunks;
    }
}