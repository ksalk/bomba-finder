using System.Text.Json;
using Bomba.DB;
using Bomba.TranscriptFetcher;
using Microsoft.EntityFrameworkCore;

var EXTRACT_SCRIPTS = true;
var CHUNK_SCRIPTS = false;
var GENERATE_EMBEDDINGS = false;
var EXTRACT_ONLY_MISSING = true;
var SHOW_SKIP_INFO = false;

// TODO: export / import scripts as JSON to avoid re-processing during development and testing

var bombaDb = new BombaDbContext();
await bombaDb.Database.EnsureCreatedAsync();

// await ScriptFinder.GetBestResultForQuery(bombaDb, "tynki gładzie i glazurka");
// return;

if (EXTRACT_SCRIPTS)
{
    Console.WriteLine("[MAIN] Starting script extraction process...");
    var videoPlaylists = new[]
    {
        //"https://www.youtube.com/playlist?list=PLHtUOYOPwzJGGZkjR-FspIL17YtSBGaCR", // Kapitan Bomba
        "https://www.youtube.com/playlist?list=PLHtUOYOPwzJEC3FKnhMtBGgXaITMR21U3", // Laserowy Gniew Dzidy
        "https://www.youtube.com/playlist?list=PLdhsyudOIKSaIGhF4ul0_eOxsoLXOwWRB" // Galaktyczne Lektury
    };

    foreach (var videoPlaylist in videoPlaylists)
    {
        await ScriptExtractor.ExtractScriptsForPlaylist(bombaDb, videoPlaylist, EXTRACT_ONLY_MISSING, SHOW_SKIP_INFO);
    }
}

// 3. Create transcript chunks and store it into DB for better context handling and retrieval
if (CHUNK_SCRIPTS)
{
    await ScriptChunker.GetScriptChunks(bombaDb);
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

    var scriptChunks = await dbContext.ScriptChunks.Where(sc => sc.Embedding == null).ToListAsync();

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
