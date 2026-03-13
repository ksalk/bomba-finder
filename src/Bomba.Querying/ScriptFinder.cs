using Bomba.DB;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using Bomba.Embeddings;

public class ScriptFinder(BombaDbContext bombaDb, OpenRouterEmbeddingService embeddingService)
{
    public async Task<SearchResult> GetBestResultForQuery(string query)
    {
        Console.WriteLine($"[FINDER] Finding best matching chunks for query: \"{query}\"");

        var queryEmbeddingTask = embeddingService.GenerateEmbeddingsAsync(new List<string> { query });

        var resultTrigrams = await FindClosestScriptChunksTrigrams(bombaDb, query);

        var queryEmbedding = await queryEmbeddingTask;
        var queryVector = queryEmbedding.First();

        var resultVectors = await FindClosestScriptChunksVectors(bombaDb, queryVector);

        Console.WriteLine();
        Console.WriteLine("[FINDER] Closest chunks using trigram similarity:");
        foreach (var result in resultTrigrams)
        {
            Console.WriteLine($"Video: {result.VideoTitle}, Similarity: {result.SimilarityScore:0.000}, Text: {result.ChunkText}");
        }

        Console.WriteLine();
        Console.WriteLine("[FINDER] Closest chunks using vector similarity:");
        foreach (var result in resultVectors)
        {
            Console.WriteLine($"Video: {result.VideoTitle}, Similarity: {result.SimilarityScore:0.000}, Text: {result.ChunkText}");
        }

        // if result trigrams and result vectors contains same chunks, list them as well in descending order 
        var commonChunks = resultTrigrams
            .Select(c => c.ChunkId)
            .Intersect(resultVectors.Select(c => c.ChunkId))
            .OrderByDescending(c =>
            {
                var trigramScore = resultTrigrams.First(r => r.ChunkId == c).SimilarityScore;
                var vectorScore = resultVectors.First(r => r.ChunkId == c).SimilarityScore;
                return (trigramScore + vectorScore) / 2; // Average score for sorting
            })
            .ToList();

        if (commonChunks.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("[FINDER] Chunks found in both trigram and vector search:");
            foreach (var chunkId in commonChunks)
            {
                var chunk = resultTrigrams.First(c => c.ChunkId == chunkId);
                var vectorChunk = resultVectors.First(c => c.ChunkId == chunkId);
                var averageScore = (chunk.SimilarityScore + vectorChunk.SimilarityScore) / 2;
                Console.WriteLine($"Video: {chunk.VideoTitle}, Average Similarity: {averageScore:0.000}, Text: {chunk.ChunkText}");
            }
        }

        SearchResult finalResult = commonChunks.Any() ? resultTrigrams.First(r => commonChunks.Contains(r.ChunkId)) : resultTrigrams.First();
        Console.WriteLine();
        Console.WriteLine("[FINDER] Final best matching chunk:");
        Console.WriteLine($"Video: {finalResult.VideoTitle}, Similarity: {finalResult.SimilarityScore:0.000}, Text: {finalResult.ChunkText}");

        return finalResult;
    }

    private async Task<List<SearchResult>> FindClosestScriptChunksVectors(BombaDbContext bombaDb, string query)
    {
        // Generate embedding for the query
        var queryEmbedding = await embeddingService.GenerateEmbeddingsAsync(new List<string> { query });
        var queryVector = queryEmbedding.First();

        return await FindClosestScriptChunksVectors(bombaDb, queryVector);
    }

    private async Task<List<SearchResult>> FindClosestScriptChunksVectors(BombaDbContext bombaDb, Vector queryVector)
    {
        // Find 5 closest script chunks using cosine distance
        var closestChunks = await bombaDb.ScriptChunks
            .Where(sc => sc.Embedding != null)
            .OrderBy(sc => sc.Embedding!.CosineDistance(queryVector))
            .Take(5)
            .Include(sc => sc.VideoScript)
            .Select(sc => new SearchResult
            {
                ChunkId = sc.Id,
                ChunkText = sc.Text,
                ChunkStartTime = sc.Start,
                VideoId = sc.VideoScript.VideoId,
                VideoTitle = sc.VideoScript.Title,
                VideoUrl = sc.VideoScript.VideoUrl,
                SimilarityScore = 1 - sc.Embedding!.CosineDistance(queryVector) // Convert distance to similarity
            })
            .ToListAsync();

        return closestChunks;
    }

    private async Task<List<SearchResult>> FindClosestScriptChunksTrigrams(BombaDbContext bombaDb, string query)
    {
        var normalizedQuery = TextNormalizer.Normalize(query);

        // Find 5 closest script chunks using trigram similarity
        var closestChunks = await bombaDb.ScriptChunks
            .Where(sc => sc.NormalizedText != null)
            .OrderByDescending(sc => EF.Functions.TrigramsSimilarity(sc.NormalizedText, normalizedQuery))
            .Take(5)
            .Include(sc => sc.VideoScript)
            .Select(sc => new SearchResult
            {
                ChunkId = sc.Id,
                ChunkText = sc.Text,
                ChunkStartTime = sc.Start,
                VideoId = sc.VideoScript.VideoId,
                VideoTitle = sc.VideoScript.Title,
                VideoUrl = sc.VideoScript.VideoUrl,
                SimilarityScore = EF.Functions.TrigramsSimilarity(sc.NormalizedText, normalizedQuery)
            })
            .ToListAsync();

        return closestChunks;
    }
}

public class SearchResult
{
    public string VideoTitle { get; set; }
    public string VideoUrl { get; set; }
    public string VideoId { get; set; }
    public Guid ChunkId { get; set; }
    public TimeSpan ChunkStartTime { get; set; }
    public string ChunkText { get; set; }
    public double SimilarityScore { get; set; }
}