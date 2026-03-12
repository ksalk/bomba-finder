
using System.Text.Json;
using Bomba.DB;
using Bomba.TranscriptFetcher;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

var EXTRACT_SCRIPTS = false;
var CHUNK_SCRIPTS = false;
var GENERATE_EMBEDDINGS = true;
var EXTRACT_ONLY_MISSING = true;
var SHOW_SKIP_INFO = false;

// TODO: export / import scripts as JSON to avoid re-processing during development and testing

var bombaDb = new BombaDbContext();
await bombaDb.Database.EnsureCreatedAsync();

if (EXTRACT_SCRIPTS)
{
    Console.WriteLine("[MAIN] Starting script extraction process...");
    var videoPlaylist = "https://www.youtube.com/playlist?list=PLHtUOYOPwzJGGZkjR-FspIL17YtSBGaCR";
    await ScriptExtractor.ExtractScriptsForPlaylist(bombaDb, videoPlaylist, EXTRACT_ONLY_MISSING, SHOW_SKIP_INFO);
}

// 3. Create transcript chunks and store it into DB for better context handling and retrieval
if (CHUNK_SCRIPTS)
{
    var allVideosInDb = bombaDb.VideoScripts.Include(vs => vs.Chunks).ToList();
    foreach (var videoScript in allVideosInDb)
    {
        var chunks = ScriptChunker.GetScriptChunks(videoScript);
        Console.WriteLine($"[MAIN] Script chunking done for video: {videoScript.VideoId}");

        videoScript.Chunks.Clear();
        videoScript.Chunks.AddRange(chunks);
        await bombaDb.SaveChangesAsync();
    }
}


// Generate embeddings for chunks using OpenRouter API
if (GENERATE_EMBEDDINGS)
{
    await GenerateAndStoreEmbeddingsAsync(bombaDb);
}

async Task GenerateAndStoreEmbeddingsAsync(BombaDbContext dbContext)
{
    Console.WriteLine("[MAIN] Starting embedding generation process...");

    OpenRouterEmbeddingService.Initialize();

    var scriptChunks = await dbContext.ScriptChunks.ToListAsync();

    var iterator = 0;
    foreach (var scriptChunk in scriptChunks.Chunk(10))
    {
        await OpenRouterEmbeddingService.GenerateEmbeddingsForScriptChunksAsync(scriptChunk.ToList());
        iterator++;

        Console.WriteLine($"[MAIN] Processed {iterator * 10}/{scriptChunks.Count} script chunks...");

    }

    Console.WriteLine("[MAIN] Embedding generation process completed.");
}

async Task ExportBombaDbToJson(string filePath)
{
    var allScripts = bombaDb.VideoScripts.ToList();
    var json = JsonSerializer.Serialize(allScripts, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(filePath, json);
}

async Task<List<SearchResult>> FindClosestScriptChunksVectors(string query)
{
    // Generate embedding for the query
    var queryEmbedding = await OpenRouterEmbeddingService.GenerateEmbeddingsAsync(new List<string> { query });
    var queryVector = queryEmbedding.First();

    await using var context = new BombaDbContext();

    // Find 5 closest script chunks using cosine distance
    var closestChunks = await context.ScriptChunks
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

async Task<List<SearchResult>> FindClosestScriptChunksTrigrams(string query)
{
    var normalizedQuery = TextNormalizer.Normalize(query);
    await using var context = new BombaDbContext();

    // TODO: search for unaccented version of the query as well to improve matching (especially for polish language)
    // Find 5 closest script chunks using trigram similarity
    var closestChunks = await context.ScriptChunks
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

async Task GetBestResultForQuery(string query)
{
    Console.WriteLine($"[MAIN] Finding best matching chunks for query: \"{query}\"");
    OpenRouterEmbeddingService.Initialize();
    var resultTrigrams = await FindClosestScriptChunksTrigrams(query);
    var resutlVectors = await FindClosestScriptChunksVectors(query);

    Console.WriteLine();
    Console.WriteLine("Closest chunks using trigram similarity:");
    foreach (var result in resultTrigrams)
    {
        Console.WriteLine($"Video: {result.VideoTitle}, Similarity: {result.SimilarityScore:0.000}, Text: {result.ChunkText}");
    }

    Console.WriteLine();
    Console.WriteLine("Closest chunks using vector similarity:");
    foreach (var result in resutlVectors)
    {
        Console.WriteLine($"Video: {result.VideoTitle}, Similarity: {result.SimilarityScore:0.000}, Text: {result.ChunkText}");
    }

    // if result trigrams and result vectors contains same chunks, list them as well in descending order 
    var commonChunks = resultTrigrams
        .Select(c => c.ChunkId)
        .Intersect(resutlVectors.Select(c => c.ChunkId))
        .OrderByDescending(c =>
        {
            var trigramScore = resultTrigrams.First(r => r.ChunkId == c).SimilarityScore;
            var vectorScore = resutlVectors.First(r => r.ChunkId == c).SimilarityScore;
            return (trigramScore + vectorScore) / 2; // Average score for sorting
        })
        .ToList();

    if (commonChunks.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Chunks found in both trigram and vector search:");
        foreach (var chunkId in commonChunks)
        {
            var chunk = resultTrigrams.First(c => c.ChunkId == chunkId);
            var vectorChunk = resutlVectors.First(c => c.ChunkId == chunkId);
            var averageScore = (chunk.SimilarityScore + vectorChunk.SimilarityScore) / 2;
            Console.WriteLine($"Video: {chunk.VideoTitle}, Average Similarity: {averageScore:0.000}, Text: {chunk.ChunkText}");
        }
    }

    SearchResult finalResult = commonChunks.Any() ? resultTrigrams.First(r => commonChunks.Contains(r.ChunkId)) : resultTrigrams.First();
    Console.WriteLine();
    Console.WriteLine("Final best matching chunk:");
    Console.WriteLine($"Video: {finalResult.VideoTitle}, Similarity: {finalResult.SimilarityScore:0.000}, Text: {finalResult.ChunkText}");
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