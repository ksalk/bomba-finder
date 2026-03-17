using Bomba.DB;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using Bomba.Embeddings;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

public class ScriptFinder(BombaDbContext bombaDb, OpenRouterEmbeddingService embeddingService, IDistributedCache? cache = null)
{
    public async Task<SearchResult> GetBestResultForQuery(string query)
    {
        var normalizedQuery = query.Trim().ToLower();
        var cacheKey = $"search:{normalizedQuery}";

        if (cache != null)
        {
            var cachedResult = await cache.GetStringAsync(cacheKey);
            if (cachedResult != null)
            {
                Console.WriteLine($"[FINDER] Cache hit for query: \"{normalizedQuery}\"");
                return JsonSerializer.Deserialize<SearchResult>(cachedResult)!;
            }
        }

        Console.WriteLine($"[FINDER] Finding best matching chunks for query: \"{normalizedQuery}\"");

        var queryEmbeddingTask = embeddingService.GenerateEmbeddingsAsync(new List<string> { normalizedQuery });

        var resultTrigrams = await FindClosestScriptChunksTrigrams(bombaDb, normalizedQuery);

        var queryEmbedding = await queryEmbeddingTask;
        var queryVector = queryEmbedding.First();

        var resultVectors = await FindClosestScriptChunksVectors(bombaDb, queryVector);

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

        SearchResult finalResult = commonChunks.Any() ? resultTrigrams.First(r => commonChunks.Contains(r.ChunkId)) : resultTrigrams.First();
        
        Console.WriteLine("[FINDER] Final best matching chunk:");
        Console.WriteLine($"Video: {finalResult.VideoTitle}, Similarity: {finalResult.SimilarityScore:0.000}, Text: {finalResult.ChunkText}");

        if (cache != null)
        {
            await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(finalResult), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7)
            });
        }

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